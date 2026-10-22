#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

if curl -fsS http://localhost:8000/health >/dev/null 2>&1; then
  echo "Port 8000 already serves an API. Run this harness in an isolated CI host." >&2
  exit 1
fi

evidence="$PWD/dashboard/test-results/deployment"
mkdir -p "$evidence/runtime/models" "$evidence/runtime/logs"
container="sharpai-dashboard-ci-${GITHUB_RUN_ID:-$$}"
server_pid=""
cleanup() {
  docker rm -f "$container" >/dev/null 2>&1 || true
  if [[ -n "$server_pid" ]]; then kill "$server_pid" 2>/dev/null || true; fi
}
trap cleanup EXIT

dotnet build src/SharpAI.Server/SharpAI.Server.csproj -c Release -f net10.0 > "$evidence/build.log" 2>&1
cat > "$evidence/runtime/sharpai.json" <<'JSON'
{
  "Rest": { "Hostname": "*", "Port": 8000 },
  "Database": { "Type": "Sqlite", "Filename": "./sharpai.db" },
  "Storage": { "ModelsDirectory": "./models/", "TempDirectory": "./temp/" },
  "Auth": { "Enabled": false },
  "Telemetry": { "Enable": false },
  "RequestHistory": { "Enabled": true },
  "Runtime": { "ForceBackend": "cpu" },
  "Logging": { "LogDirectory": "./logs/", "ConsoleLogging": true }
}
JSON
server_dll="$PWD/src/SharpAI.Server/bin/Release/net10.0/SharpAI.Server.dll"
(cd "$evidence/runtime" && exec env SHARPAI_FORCE_BACKEND=cpu SHARPAI_REQUIRE_BACKEND=true dotnet "$server_dll") > "$evidence/server.log" 2>&1 &
server_pid=$!
for attempt in $(seq 1 60); do
  kill -0 "$server_pid" 2>/dev/null || { cat "$evidence/server.log"; exit 1; }
  if curl -fsS http://localhost:8000/ready >/dev/null 2>&1; then break; fi
  sleep 1
done
curl -fsS http://localhost:8000/ready > "$evidence/readiness.json"
docker build -f dashboard/Dockerfile -t "$container" dashboard > "$evidence/docker-build.log" 2>&1
docker run -d --name "$container" --add-host sharpai:host-gateway -p 3017:3000 "$container" > "$evidence/container.txt"
for attempt in $(seq 1 30); do
  if curl -fsS http://localhost:3017/health >/dev/null 2>&1; then break; fi
  sleep 1
done
python3 scripts/test-dashboard-deployment.py --url http://localhost:3017 | tee "$evidence/assertions.log"
