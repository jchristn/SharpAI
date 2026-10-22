#!/usr/bin/env bash
# Run deterministic, simulated HTTP, real-model, live API, and authorization checks.
# Requires dotnet 8/10, Python 3, curl, and Postman CLI >= 1.70.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$(mktemp -d "${TMPDIR:-/tmp}/sharpai-operations.XXXXXX")}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"
RUNTIME="$(mktemp -d "${TMPDIR:-/tmp}/sharpai-runtime.XXXXXX")"
SIM_PORT="${SHARPAI_TEST_SIM_PORT:-4917}"
SERVER_PORT="${SHARPAI_TEST_API_PORT:-4918}"
MODEL="${SHARPAI_TEST_MODEL_GGUF:?Set SHARPAI_TEST_MODEL_GGUF to real GGUF weights}"
MODEL="$(python3 -c 'import os,sys; print(os.path.realpath(sys.argv[1]))' "$MODEL")"
SERVER_PID=""
cleanup() {
    if [[ -n "$SERVER_PID" ]]; then kill -TERM "$SERVER_PID" 2>/dev/null || true; wait "$SERVER_PID" 2>/dev/null || true; fi
    # The Postman launcher does not forward termination reliably; scope shutdown to this unique config.
    pkill -INT -f "simulation run $RUNTIME/downloads.sim.yaml" 2>/dev/null || true
    rm -rf "$RUNTIME"
}
trap cleanup EXIT
cd "$ROOT"
# Small fixtures with multiple slots should not create a full machine-sized thread pool per slot.
export SHARPAI_NUM_THREADS="${SHARPAI_NUM_THREADS:-2}"
export SHARPAI_BATCH_THREADS="${SHARPAI_BATCH_THREADS:-2}"
command -v postman >/dev/null
python3 - "$ROOT" "$RUNTIME" "$SERVER_PORT" <<'PY'
import json, pathlib, sys
root, runtime, port = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2]), int(sys.argv[3])
settings = json.loads((root / 'docker/sharpai.json').read_text())
settings['Rest']['Hostname'] = '127.0.0.1'
settings['Rest']['Port'] = port
settings['Telemetry'] = {'Enable': False}
settings['Auth'] = {'Enabled': False}
settings['HuggingFace'] = {'ApiKey': None}
settings['Logging']['ConsoleLogging'] = False
(runtime / 'sharpai.json').write_text(json.dumps(settings))
config = str(root / 'postman/mocks/huggingface/config.yaml')
(runtime / 'downloads.sim.yaml').write_text('simulation: sharpai-tests\nrouting: path\nmocks:\n  - routeKey: hf\n    path: ' + json.dumps(config) + '\n')
PY
python3 scripts/split-test-gguf.py "$MODEL" "$RUNTIME/real-shards" > "$OUT/split-fixture.txt"
export SHARPAI_TEST_SPLIT_DIRECTORY="$RUNTIME/real-shards"
postman simulation run "$RUNTIME/downloads.sim.yaml" --port "$SIM_PORT" --output ndjson > "$OUT/simulation.ndjson" 2> "$OUT/simulation.err" &
wait_for() {
    for attempt in {1..100}; do
        if curl --fail --silent "$1" >/dev/null; then return; fi
        sleep 0.1
    done
    echo "Service did not become ready: $1" >&2
    return 1
}
wait_for "http://127.0.0.1:$SIM_PORT/hf/health"
dotnet build src/SharpAI.sln > "$OUT/build.log" 2>&1
dotnet build sdk/csharp/src/SharpAI.Sdk.sln > "$OUT/sdk-build.log" 2>&1
export SHARPAI_TEST_WORKER_DLL="$ROOT/src/Test.Automated/bin/Debug/net8.0/Test.Automated.dll"
start_server() {
    (cd "$RUNTIME"; exec env SHARPAI_FORCE_BACKEND=cpu SHARPAI_CONTEXT_SIZE=512 dotnet "$ROOT/src/SharpAI.Server/bin/Debug/net8.0/SharpAI.Server.dll") > "$OUT/server.log" 2>&1 &
    SERVER_PID=$!
    wait_for "http://127.0.0.1:$SERVER_PORT/health"
}
start_server
python3 - "$MODEL" > "$RUNTIME/import.json" <<'PY'
import json, sys
print(json.dumps({'path': sys.argv[1], 'name': 'ops-story'}))
PY
curl --fail --silent -H 'Content-Type: application/json' --data-binary "@$RUNTIME/import.json" "http://127.0.0.1:$SERVER_PORT/api/import" > "$OUT/import.json"
for framework in net8.0 net10.0; do
    env -u SHARPAI_TEST_MODEL_GGUF SHARPAI_TEST_HF_URL="http://127.0.0.1:$SIM_PORT/hf" SHARPAI_TEST_SERVER_URL="http://127.0.0.1:$SERVER_PORT" \
        dotnet test src/Test.Xunit/Test.Xunit.csproj -f "$framework" --collect:'XPlat Code Coverage' --results-directory "$OUT/$framework/contracts"
    SHARPAI_TEST_MODEL_GGUF="$MODEL" dotnet test src/Test.Xunit/Test.Xunit.csproj -f "$framework" --no-restore \
        --filter 'FullyQualifiedName~NativeOperationsTests|FullyQualifiedName~EngineReliabilityTests' --collect:'XPlat Code Coverage' --results-directory "$OUT/$framework/native"
done
for framework in net8.0 net10.0; do
    dotnet run --project sdk/csharp/src/Test.Automated -f "$framework" --no-build -- --contracts "http://127.0.0.1:$SERVER_PORT" > "$OUT/sdk-$framework.log"
done
SHARPAI_TEST_SERVER_URL="http://127.0.0.1:$SERVER_PORT" python3 -m unittest discover -s sdk/python/tests -p 'test_*.py'
# Check ordinary live server contracts, then authentication and administrator-only diagnostics.
dotnet run --project src/Test.Server -- "http://127.0.0.1:$SERVER_PORT" --results "$OUT/server-contracts.json"
kill -TERM "$SERVER_PID"; wait "$SERVER_PID"; SERVER_PID=""
python3 - "$RUNTIME/sharpai.json" <<'PY'
import json, sys
path = sys.argv[1]
settings = json.load(open(path))
settings['Auth'] = {'Enabled': True, 'AdminApiKeys': ['test-admin-key']}
json.dump(settings, open(path, 'w'))
PY
start_server
dotnet run --project src/Test.Server -- "http://127.0.0.1:$SERVER_PORT" --auth --results "$OUT/auth-contracts.json"
python3 scripts/coverage-summary.py "$OUT" --minimum-operations-line 100 --minimum-operations-branch 90 --output "$OUT/summary.json"
python3 scripts/test-hardware-memory.py --model "$MODEL" --backend "${SHARPAI_TEST_EXPECTED_BACKEND:-cpu}" --output "$OUT/memory-matrix"
python3 -m unittest discover -s scripts -p 'test_*.py'
if [[ "${SHARPAI_TEST_OBSERVABILITY:-0}" == "1" ]]; then
    python3 scripts/test-observability.py --coverage --model "$MODEL" --output "$OUT/observability" --expected-backend "${SHARPAI_TEST_EXPECTED_BACKEND:-cpu}"
fi
echo "Results: $OUT"
