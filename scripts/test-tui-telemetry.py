#!/usr/bin/env python3
"""Verify the real client Radiant lifecycle through an isolated Prometheus/Loki/Tempo/Grafana stack."""
import argparse
import json
import pathlib
import socket
import subprocess
import tempfile
import time
import urllib.parse
import urllib.request
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[1]


def request(url):
    with urllib.request.urlopen(url, timeout=5) as response:
        return json.load(response)


def eventually(action, label):
    deadline = time.monotonic() + 90
    while time.monotonic() < deadline:
        try:
            result = action()
            if result:
                return result
        except (OSError, ValueError, KeyError):
            pass
        time.sleep(1)
    raise AssertionError('No evidence for ' + label)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--api', required=True, help='Isolated SharpAI server; only anonymous /health is requested')
    parser.add_argument('--output', type=pathlib.Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='sharpai-tui-telemetry-') as directory:
        runtime = pathlib.Path(directory)
        probes = [socket.socket() for _ in range(6)]
        for probe in probes:
            probe.bind(('127.0.0.1', 0))
        otlp, collector, prometheus, loki, tempo, grafana = [probe.getsockname()[1] for probe in probes]
        telemetry = ROOT / 'docker/telemetry'
        (runtime / 'prometheus.json').write_text(json.dumps({'global': {'scrape_interval': '1s'}, 'scrape_configs': [{'job_name': 'tui', 'static_configs': [{'targets': ['otel-collector:8889']}]}]}))
        logs = runtime / 'logs'
        logs.mkdir()
        services = {
            'otel-collector': {'image': 'otel/opentelemetry-collector-contrib:0.109.0', 'command': ['--config=/etc/config.yaml'], 'volumes': [f'{telemetry}/otel-collector-config.yaml:/etc/config.yaml:ro', f'{logs}:/app/logs:ro'], 'ports': [f'127.0.0.1:{otlp}:4317', f'127.0.0.1:{collector}:8889']},
            'prometheus': {'image': 'prom/prometheus:v2.55.1', 'command': ['--config.file=/etc/prometheus/config.json'], 'volumes': [f'{runtime}/prometheus.json:/etc/prometheus/config.json:ro'], 'ports': [f'127.0.0.1:{prometheus}:9090']},
            'loki': {'image': 'grafana/loki:3.2.1', 'command': ['-config.file=/etc/loki/config.yaml'], 'volumes': [f'{telemetry}/loki-config.yaml:/etc/loki/config.yaml:ro'], 'ports': [f'127.0.0.1:{loki}:3100']},
            'tempo': {'image': 'grafana/tempo:2.6.1', 'command': ['-config.file=/etc/tempo.yaml'], 'volumes': [f'{telemetry}/tempo.yaml:/etc/tempo.yaml:ro'], 'ports': [f'127.0.0.1:{tempo}:3200']},
            'grafana': {'image': 'grafana/grafana:11.3.0', 'environment': {'GF_AUTH_ANONYMOUS_ENABLED': 'true', 'GF_AUTH_ANONYMOUS_ORG_ROLE': 'Admin'}, 'volumes': [f'{telemetry}/grafana/provisioning:/etc/grafana/provisioning:ro', f'{telemetry}/grafana/dashboards:/var/lib/grafana/dashboards:ro'], 'ports': [f'127.0.0.1:{grafana}:3000']},
        }
        compose = runtime / 'compose.json'
        compose.write_text(json.dumps({'services': services}))
        command = ['docker', 'compose', '-p', 'sharpai-tui-' + uuid.uuid4().hex[:10], '-f', str(compose)]
        for probe in probes:
            probe.close()
        with (output / 'compose.log').open('w') as log:
            subprocess.run(command + ['up', '-d'], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, check=True)
        try:
            eventually(lambda: request(f'http://127.0.0.1:{grafana}/api/health'), 'Grafana readiness')
            subprocess.run(['dotnet', 'run', '--project', 'src/Test.Tui', '-f', 'net8.0', '--', '--telemetry-probe', f'http://127.0.0.1:{otlp}', str(logs), args.api, str(runtime / 'trace')], cwd=ROOT, check=True)
            trace_id = (runtime / 'trace').read_text()
            def metrics():
                query = urllib.parse.urlencode({'query': '{__name__=~"sharpai_(client|tui).*"}'})
                series = request(f'http://127.0.0.1:{prometheus}/api/v1/query?{query}')['data']['result']
                names = [item['metric']['__name__'] for item in series]
                return names if any('client_request_duration' in name for name in names) and any('tui_failures' in name for name in names) else None
            names = eventually(metrics, 'client request and failure instruments')
            query = urllib.parse.urlencode({'query': '{service_name="sharpai-tui"}', 'start': time.time_ns() - 300_000_000_000, 'limit': 100})
            log_result = eventually(lambda: request(f'http://127.0.0.1:{loki}/loki/api/v1/query_range?{query}')['data']['result'], 'Loki client records')
            trace = eventually(lambda: request(f'http://127.0.0.1:{tempo}/api/traces/{trace_id}'), 'Tempo client trace')
            serialized = json.dumps(trace)
            assert 'http.request' in serialized and 'screen.open' in serialized
            sources = request(f'http://127.0.0.1:{grafana}/api/datasources')
            assert {'prometheus', 'loki', 'tempo'} <= {source['uid'] for source in sources}
            (output / 'evidence.json').write_text(json.dumps({'traceId': trace_id, 'metrics': names, 'lokiStreams': len(log_result), 'grafanaDatasources': [source['uid'] for source in sources], 'passed': True}, indent=2))
        finally:
            with (output / 'containers.log').open('w') as log:
                subprocess.run(command + ['logs', '--no-color'], stdout=log, stderr=subprocess.STDOUT)
            subprocess.run(command + ['down', '-v'], check=True)


if __name__ == '__main__':
    main()
