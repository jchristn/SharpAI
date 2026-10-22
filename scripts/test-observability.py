#!/usr/bin/env python3
"""Exercise the built server against real Prometheus/Loki/Tempo/Grafana containers.
Uses isolated ports, logs, database and Compose project; never touches an existing stack.
"""
import argparse
import json
import os
import pathlib
import re
import signal
import shutil
import xml.etree.ElementTree as ET
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[1]


def request(url, payload=None, headers=None, method=None, timeout=10):
    data = None if payload is None else json.dumps(payload).encode()
    req = urllib.request.Request(url, data, {'Content-Type': 'application/json', **(headers or {})}, method=method)
    with urllib.request.urlopen(req, timeout=timeout) as response:
        return json.load(response)


def eventually(check, description, timeout=120):
    end = time.monotonic() + timeout
    last = None
    while time.monotonic() < end:
        try:
            result = check()
            if result:
                return result
        except (OSError, ValueError, KeyError, AssertionError) as error:
            last = error
        time.sleep(1)
    raise AssertionError(f'{description} did not become ready: {last}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--framework', choices=['net8.0', 'net10.0'], default='net8.0')
    parser.add_argument('--coverage', action='store_true', help='Collect server coverage with the pinned local dotnet tool')
    parser.add_argument('--expected-backend', choices=['cpu', 'metal', 'cuda'], default='cpu')
    args = parser.parse_args()
    output = pathlib.Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='sharpai-observability-') as temp:
        runtime = pathlib.Path(temp)
        logs = runtime / 'logs'
        logs.mkdir()
        # Hold reservations until all ports are selected, avoiding duplicate ephemeral ports.
        reservations = [socket.socket() for _ in range(7)]
        for sock in reservations:
            sock.bind(('127.0.0.1', 0))
        api, otlp, collector, prometheus, loki, tempo, grafana = [sock.getsockname()[1] for sock in reservations]
        telemetry = ROOT / 'docker/telemetry'
        prom_config = {'global': {'scrape_interval': '1s'}, 'scrape_configs': [
            {'job_name': 'otel-collector', 'static_configs': [{'targets': ['otel-collector:8889']}]}]}
        (runtime / 'prometheus.json').write_text(json.dumps(prom_config))
        services = {
            'otel-collector': {'image': 'otel/opentelemetry-collector-contrib:0.109.0',
                'command': ['--config=/etc/config.yaml'], 'volumes': [f'{telemetry}/otel-collector-config.yaml:/etc/config.yaml:ro', f'{logs}:/app/logs:ro'],
                'ports': [f'127.0.0.1:{otlp}:4317', f'127.0.0.1:{collector}:8889']},
            'prometheus': {'image': 'prom/prometheus:v2.55.1', 'command': ['--config.file=/etc/prometheus/config.json'],
                'volumes': [f'{runtime}/prometheus.json:/etc/prometheus/config.json:ro'], 'ports': [f'127.0.0.1:{prometheus}:9090']},
            'loki': {'image': 'grafana/loki:3.2.1', 'command': ['-config.file=/etc/loki/config.yaml'],
                'volumes': [f'{telemetry}/loki-config.yaml:/etc/loki/config.yaml:ro'], 'ports': [f'127.0.0.1:{loki}:3100']},
            'tempo': {'image': 'grafana/tempo:2.6.1', 'command': ['-config.file=/etc/tempo.yaml'],
                'volumes': [f'{telemetry}/tempo.yaml:/etc/tempo.yaml:ro'], 'ports': [f'127.0.0.1:{tempo}:3200']},
            'grafana': {'image': 'grafana/grafana:11.3.0',
                'environment': {'GF_AUTH_ANONYMOUS_ENABLED': 'true', 'GF_AUTH_ANONYMOUS_ORG_ROLE': 'Admin'},
                'volumes': [f'{telemetry}/grafana/provisioning:/etc/grafana/provisioning:ro', f'{telemetry}/grafana/dashboards:/var/lib/grafana/dashboards:ro'],
                'ports': [f'127.0.0.1:{grafana}:3000']}}
        compose = runtime / 'compose.json'
        compose.write_text(json.dumps({'services': services}))
        command = ['docker', 'compose', '-p', 'sharpai-test-' + uuid.uuid4().hex[:10], '-f', str(compose)]
        for sock in reservations:
            sock.close()
        settings = json.loads((ROOT / 'docker/sharpai.json').read_text())
        settings['Rest'].update(Hostname='127.0.0.1', Port=api)
        settings['Auth'] = {'Enabled': False}
        settings['Telemetry'] = {'Enable': True, 'ServiceName': 'sharpai', 'OtlpEndpoint': f'http://127.0.0.1:{otlp}',
            'OtlpProtocol': 'grpc', 'PrometheusEnable': False, 'EnableMetrics': True, 'EnableTraces': True}
        settings['Logging']['ConsoleLogging'] = True
        (runtime / 'sharpai.json').write_text(json.dumps(settings))
        server = None
        coverage_session = None
        started = time.time_ns()
        evidence = {'framework': args.framework, 'backend_requested': args.expected_backend, 'checks': []}
        def passed(name):
            evidence['checks'].append(name)
            print('PASS:', name, flush=True)
        try:
            with (output / 'compose.log').open('w') as log:
                subprocess.run(command + ['up', '-d', '--wait', '--wait-timeout', '120'], stdout=log, stderr=subprocess.STDOUT, check=True, timeout=300)
            eventually(lambda: request(f'http://127.0.0.1:{grafana}/api/health'), 'Grafana')
            env = dict(os.environ, SHARPAI_FORCE_BACKEND=args.expected_backend, SHARPAI_REQUIRE_BACKEND='true',
                OTEL_METRIC_EXPORT_INTERVAL='1000', SHARPAI_CONTEXT_SIZE='256', SHARPAI_MAX_CONCURRENT_GENERATIONS='2', SHARPAI_ENABLE_NATIVE_LOGGING='true')
            # Explicit settings above control export. Do not inherit a developer's telemetry override.
            env = {k: v for k, v in env.items() if not k.startswith('SHARPAI_TELEMETRY_')}
            server_command = ['dotnet', str(ROOT / 'src/SharpAI.Server/bin/Debug' / args.framework / 'SharpAI.Server.dll')]
            coverage_tool = ['dotnet', 'tool', 'run', 'dotnet-coverage']
            if args.coverage:
                subprocess.run(['dotnet', 'tool', 'restore'], cwd=ROOT, check=True, timeout=120)
                coverage_session = 'sharpai-' + uuid.uuid4().hex
                subprocess.run(coverage_tool + ['collect', '--server-mode', '--background', '--session-id', coverage_session,
                    '--output', str(output / 'server.cobertura.xml'), '--output-format', 'cobertura'], cwd=ROOT, check=True, timeout=30)
                # Static instrumentation also works on macOS ARM64, where dynamic profiling is unavailable.
                # Instrument isolated assembly copies, never the developer's build outputs.
                instrumented = runtime / 'instrumented'
                instrumented.mkdir()
                for source in (ROOT / 'src/SharpAI.Server/bin/Debug' / args.framework).iterdir():
                    target = instrumented / source.name
                    if source.is_dir():
                        target.symlink_to(source, target_is_directory=True)
                    else:
                        shutil.copy2(source, target)
                for assembly in ['SharpAI.Server.dll', 'SharpAI.dll']:
                    subprocess.run(coverage_tool + ['instrument', str(instrumented / assembly), '--session-id', coverage_session],
                        cwd=ROOT, check=True, timeout=30)
                server_command = ['dotnet', str(instrumented / 'SharpAI.Server.dll')]
                # Resolve the same local tool while keeping app state in its isolated directory.
                (runtime / '.config').symlink_to(ROOT / '.config', target_is_directory=True)
                server_command = coverage_tool + ['connect', coverage_session] + server_command
            with (output / 'server.log').open('w') as server_log:
                server = subprocess.Popen(server_command, cwd=runtime,
                    env=env, stdout=server_log, stderr=subprocess.STDOUT, start_new_session=True)
                base = f'http://127.0.0.1:{api}'
                eventually(lambda: request(base + '/health'), 'SharpAI')
                request(base + '/api/import', {'path': str(pathlib.Path(args.model).resolve()), 'name': 'ops-story'})
                # Explicit W3C parent makes correlation verifiable across storage backends.
                trace_id = uuid.uuid4().hex
                result = request(base + '/v1/completions', {'model': 'ops-story', 'prompt': 'Once upon a time', 'max_tokens': 8, 'temperature': 0},
                    {'traceparent': f'00-{trace_id}-0123456789abcdef-01'})
                assert result['usage']['completion_tokens'] == 8
                passed('live OpenAI generation and token usage')
                with (output / 'server-tests.log').open('w') as contract_log:
                    subprocess.run(['dotnet', 'test', 'src/Test.Xunit/Test.Xunit.csproj', '-f', args.framework, '--no-build',
                        '--filter', 'FullyQualifiedName~ServerOperationsTests'], cwd=ROOT,
                        env=dict(env, SHARPAI_TEST_SERVER_URL=base, SHARPAI_TEST_EXPECTED_BACKEND=args.expected_backend), stdout=contract_log, stderr=subprocess.STDOUT, check=True, timeout=120)
                passed('live server usage, stream, overflow, inspection and diagnostics contracts')
                diag = request(base + '/api/diagnostics/runtime')
                assert diag['backend_selected'] == args.expected_backend, diag
                evidence['runtime'] = diag
                # Read the actual inference span ID from the correlated rolling log. Some HTTP hosts
                # do not propagate traceparent; use engine-emitted identity as the authoritative one.
                def operation_log():
                    for path in logs.glob('*.log*'):
                        for line in path.read_text(errors='replace').splitlines():
                            if '"event_name":"engine.inference.completed"' in line:
                                record = json.loads(line[line.index('{'):])
                                if record.get('trace_id'):
                                    return record
                    return None
                operation = eventually(operation_log, 'correlated engine operation log')
                trace_id = operation['trace_id']
                evidence['operation'] = operation
                expected = [
                    'sharpai_inference_requests', 'sharpai_inference_tokens_generated', 'sharpai_inference_latency',
                    'sharpai_models_load_duration', 'sharpai_inference_queue_wait', 'sharpai_inference_time_to_first_token',
                    'sharpai_inference_prompt_eval_duration', 'sharpai_inference_generation_duration',
                    'sharpai_inference_prompt_tokens_per_second', 'sharpai_inference_generation_tokens_per_second',
                    'sharpai_inference_busy_slots', 'sharpai_inference_total_slots', 'sharpai_inference_cancellations_total']
                def exported_metrics():
                    series = request(f'http://127.0.0.1:{prometheus}/api/v1/query?' + urllib.parse.urlencode({'query': '{__name__=~"sharpai_.*"}'}))['data']['result']
                    names = {item['metric']['__name__'] for item in series}
                    if all(any(name.startswith(prefix) for name in names) for prefix in expected):
                        return sorted(names)
                evidence['metrics'] = eventually(exported_metrics, 'all engine instruments in Prometheus')
                passed('engine metrics exported through OTLP collector into Prometheus')
                # A second observation after the first export gives rate queries a real delta.
                request(base + '/v1/completions', {'model': 'ops-story', 'prompt': 'Once upon a time', 'max_tokens': 8, 'temperature': 0})
                dashboard = request(f'http://127.0.0.1:{grafana}/api/dashboards/uid/sharpai-engine')['dashboard']
                # Execute each provisioned expression through Grafana's Prometheus datasource proxy.
                for panel in dashboard['panels']:
                    if panel['type'] != 'timeseries':
                        continue
                    expression = panel['targets'][0]['expr'].replace('$__rate_interval', '5m')
                    def panel_data():
                        series = request(f'http://127.0.0.1:{grafana}/api/datasources/proxy/uid/prometheus/api/v1/query?' +
                        urllib.parse.urlencode({'query': expression}))['data']['result']
                        return series if series and all(item['value'][1] not in ['NaN', '+Inf', '-Inf'] for item in series) else None
                    eventually(panel_data, panel['title'])
                passed('provisioned Grafana engine panel queries return finite data')
                query = '{service_name="sharpai"} |= "' + trace_id + '"'
                evidence['loki'] = eventually(lambda: request(f'http://127.0.0.1:{loki}/loki/api/v1/query_range?' +
                    urllib.parse.urlencode({'query': query, 'start': started, 'end': time.time_ns(), 'limit': 100}))['data']['result'], 'Loki correlated operation')
                passed('engine operation and trace ID ingested into Loki')
                trace = eventually(lambda: request(f'http://127.0.0.1:{tempo}/api/traces/{trace_id}'), 'Tempo engine trace')
                text = json.dumps(trace)
                for attribute in ['sharpai.prompt_tokens', 'sharpai.completion_tokens', 'sharpai.ttft_ns', 'sharpai.queue_wait_ns', 'sharpai.timing_source']:
                    assert attribute in text, attribute
                evidence['trace'] = trace
                passed('Tempo stores inference span with measured phase attributes')
                sources = request(f'http://127.0.0.1:{grafana}/api/datasources')
                by_uid = {source['uid']: source for source in sources}
                assert by_uid['loki']['jsonData']['derivedFields'][0]['datasourceUid'] == 'tempo'
                field = by_uid['loki']['jsonData']['derivedFields'][0]
                assert field['url'] == '${__value.raw}'
                assert re.search(field['matcherRegex'], json.dumps(operation, separators=(',', ':'))).group(1) == trace_id
                assert by_uid['tempo']['jsonData']['tracesToLogsV2']['filterByTraceID']
                passed('Grafana log-to-trace and trace-to-log correlation provisioned')
                if args.coverage:
                    subprocess.run(coverage_tool + ['snapshot', coverage_session, '--output', str(output / 'server-snapshot.cobertura.xml')], cwd=ROOT, check=True, timeout=30)
                    coverage = ET.parse(output / 'server-snapshot.cobertura.xml')
                    hits = [line for cls in coverage.findall('.//class') if 'SharpAI.Server' in cls.attrib.get('name', '')
                        for line in cls.findall('./lines/line') if int(line.attrib.get('hits', 0)) > 0]
                    assert hits, 'No instrumented server lines were exercised'
                    evidence['server_covered_lines'] = len(hits)
                    passed('direct server instrumentation records exercised handler lines')
                if args.expected_backend != 'cpu':
                    text = (output / 'server.log').read_text(errors='replace')
                    assert re.search(r'offloaded [1-9][0-9]*/[0-9]+ layers to GPU', text), 'Native logs did not prove GPU layer offload'
                    evidence['memory'] = request(base + '/api/diagnostics/memory')
                    passed('native GPU offload confirmed; memory diagnostics captured')
        finally:
            if server is not None and server.poll() is None:
                os.killpg(server.pid, signal.SIGTERM)
                try:
                    server.wait(timeout=20)
                except subprocess.TimeoutExpired:
                    os.killpg(server.pid, signal.SIGKILL)
                    server.wait()
            if coverage_session:
                subprocess.run(coverage_tool + ['shutdown', coverage_session], cwd=ROOT, check=True, timeout=30)
            (output / 'evidence.json').write_text(json.dumps(evidence, indent=2))
            with (output / 'containers.log').open('w') as log:
                subprocess.run(command + ['logs', '--no-color'], stdout=log, stderr=subprocess.STDOUT, timeout=30)
            subprocess.run(command + ['down', '--volumes'], check=True, timeout=60)
    print('Evidence:', output)


if __name__ == '__main__':
    main()
