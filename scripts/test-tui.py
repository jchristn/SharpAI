#!/usr/bin/env python3
"""Run the TUI against Postman transport fixtures and an isolated native SharpAI server."""
import argparse
import hashlib
import json
import os
import pathlib
import signal
import shutil
import socket
import subprocess
import tempfile
import time
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]


def port():
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0))
        return probe.getsockname()[1]


def wait(url, process):
    for _ in range(300):
        if process.poll() is not None:
            raise RuntimeError(f'Service exited before readiness: {url}')
        try:
            with urllib.request.urlopen(url, timeout=1):
                return
        except (OSError, TimeoutError):
            time.sleep(.1)
    raise RuntimeError(f'Service did not become ready: {url}')


def simulation_url(log, process):
    deadline = time.monotonic() + 30
    while time.monotonic() < deadline:
        for line in log.read_text().splitlines():
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                continue
            if event.get('event') == 'listening':
                return event['url'].rstrip('/') + '/server'
        if process.poll() is not None:
            raise RuntimeError('Postman simulation exited before readiness; see simulation.err')
        time.sleep(0.1)
    raise RuntimeError('Postman simulation did not report its assigned port')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', type=pathlib.Path, required=True)
    parser.add_argument('--pull-url', help='Also verify an actual remote GGUF pull through the server')
    parser.add_argument('--benchmark-baseline', type=pathlib.Path, help='Compare with a previous matching-runtime benchmark')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'artifacts/tui')
    args = parser.parse_args()
    model = args.model.resolve()
    assert model.is_file(), 'Native validation requires actual GGUF weights'
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    subprocess.run(['dotnet', 'build', 'src/SharpAI.sln', '-v', 'quiet'], cwd=ROOT, check=True)
    subprocess.run(['python3', 'scripts/check-tui.py'], cwd=ROOT, check=True)
    with tempfile.TemporaryDirectory(prefix='sharpai-tui-') as directory:
        runtime = pathlib.Path(directory)
        server_port = port()
        settings = json.loads((ROOT / 'docker/sharpai.json').read_text())
        settings['Rest'].update({'Hostname': '127.0.0.1', 'Port': server_port})
        settings['Telemetry'] = {'Enable': False}
        settings['Logging']['ConsoleLogging'] = False
        settings['Database'] = {'Type': 'Sqlite', 'Filename': './tui.db'}
        (runtime / 'sharpai.json').write_text(json.dumps(settings))
        simulation = runtime / 'tui.sim.yaml'
        simulation.write_text('simulation: sharpai-tui-tests\nrouting: path\nmocks:\n  - routeKey: server\n    path: ' + json.dumps(str(ROOT / 'postman/mocks/tui/config.yaml')) + '\n')
        sim_log = open(output / 'simulation.ndjson', 'w')
        sim_error = open(output / 'simulation.err', 'w')
        server_log = open(output / 'server.log', 'w')
        sim = subprocess.Popen([shutil.which('postman') or shutil.which('postman.cmd') or 'postman', 'simulation', 'run', str(simulation), '--port', 'auto', '--output', 'ndjson'], stdout=sim_log, stderr=sim_error, start_new_session=os.name != 'nt')
        server = None
        try:
            sim_url = simulation_url(output / 'simulation.ndjson', sim)
            wait(sim_url + '/health', sim)
            env = {**os.environ, 'SHARPAI_FORCE_BACKEND': 'cpu', 'SHARPAI_NUM_THREADS': '2', 'SHARPAI_BATCH_THREADS': '2', 'SHARPAI_CONTEXT_SIZE': '512'}
            command = ['dotnet', str(ROOT / 'src/SharpAI.Server/bin/Debug/net8.0/SharpAI.Server.dll')]
            server = subprocess.Popen(command, cwd=runtime, env=env, stdout=server_log, stderr=subprocess.STDOUT)
            base = f'http://127.0.0.1:{server_port}'
            wait(base + '/health', server)
            request = urllib.request.Request(base + '/v1.0/token', method='POST', headers={'x-email': 'admin@sharpai.local', 'x-password': 'password'})
            with urllib.request.urlopen(request) as response:
                token = json.load(response)['token']
            # Validate the production starter before choosing an explicit small-fixture budget.
            headers = {'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'}
            starter_url = base + '/v1.0/model-configurations/system-starter-configuration'
            with urllib.request.urlopen(urllib.request.Request(starter_url, headers=headers)) as response:
                starter = json.load(response)
            assert starter['Configuration']['MaxTokens'] == 2048, 'Upgraded starter budget'
            starter['Configuration']['MaxTokens'] = 128
            body = {'Name': starter['Name'], 'Scope': starter['Scope'], 'Configuration': starter['Configuration']}
            with urllib.request.urlopen(urllib.request.Request(starter_url, data=json.dumps(body).encode(), headers=headers, method='PUT')) as response:
                assert response.status == 200
            token_file = runtime / 'session'
            token_file.write_text(token)
            if os.name != 'nt':
                token_file.chmod(0o600)
            request = urllib.request.Request(base + '/api/import', data=json.dumps({'path': str(model), 'name': 'tui-stories'}).encode(), headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
            with urllib.request.urlopen(request) as response:
                assert response.status == 200
            if args.pull_url:
                request = urllib.request.Request(base + '/api/pull', data=json.dumps({'model': args.pull_url, 'stream': True}).encode(), headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
                with urllib.request.urlopen(request, timeout=180) as response:
                    updates = [json.loads(line) for line in response if line.strip()]
                (output / 'live-pull.json').write_text(json.dumps(updates, indent=2))
                assert updates and updates[-1].get('status') == 'success', 'Real remote pull must confirm verified publication'
                request = urllib.request.Request(base + '/api/chat', data=json.dumps({'model': args.pull_url, 'stream': False, 'messages': [{'role': 'user', 'content': 'Once upon a time'}], 'options': {'num_predict': 8, 'temperature': 0}}).encode(), headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
                with urllib.request.urlopen(request, timeout=180) as response:
                    generated = json.load(response)
                assert generated.get('done') and generated.get('eval_count', 0) > 0, 'Downloaded model must generate through its original registered URL'
                (output / 'live-pull-inference.json').write_text(json.dumps({'passed': True, 'outputTokens': generated['eval_count']}, indent=2))
            env.update({'SHARPAI_TUI_TEST_URL': sim_url, 'SHARPAI_TUI_LIVE_URL': base, 'SHARPAI_TUI_TOKEN_FILE': str(token_file), 'SHARPAI_TUI_MODEL': 'tui-stories', 'SHARPAI_TUI_MODEL_SHA256': hashlib.sha256(model.read_bytes()).hexdigest(), 'SHARPAI_TUI_RECORDINGS': str(output / 'frames')})
            for framework in ['net8.0', 'net10.0']:
                subprocess.run(['dotnet', 'run', '--project', 'src/Test.Tui', '-f', framework, '--', '--results', str(output / f'{framework}.json')], cwd=ROOT, env={**env, 'SHARPAI_TUI_RECORDINGS': str(output / framework / 'frames')}, check=True)
            # Reopen the same SQLite server and verify persisted history measurements through the public API.
            server.terminate()
            server.wait(timeout=15)
            server = subprocess.Popen(command, cwd=runtime, env=env, stdout=server_log, stderr=subprocess.STDOUT)
            wait(base + '/health', server)
            request = urllib.request.Request(base + '/v1.0/token', method='POST', headers={'x-email': 'admin@sharpai.local', 'x-password': 'password'})
            with urllib.request.urlopen(request) as response:
                token = json.load(response)['token']
            token_file.write_text(token)
            request = urllib.request.Request(base + '/v1.0/api/request-history?category=inference&pageSize=25', headers={'Authorization': 'Bearer ' + token})
            with urllib.request.urlopen(request) as response:
                history = json.load(response)
            assert any(row.get('OutputTokens', 0) and row.get('OutputTokens', 0) > 0 for row in history['Objects']), 'Native inference usage survives restart'
            (output / 'restart.json').write_text(json.dumps({'passed': True, 'records': len(history['Objects'])}, indent=2))
            benchmark = ['dotnet', 'run', '--project', 'src/Test.Benchmark', '-f', 'net8.0', '--', str(output / 'benchmark.json')]
            if args.benchmark_baseline:
                benchmark.append(str(args.benchmark_baseline.resolve()))
            subprocess.run(benchmark, cwd=ROOT, env=env, check=True)
            subprocess.run(['dotnet', 'run', '--project', 'src/Test.Benchmark', '-f', 'net8.0', '--', str(output / 'benchmark-repeat.json'), str(output / 'benchmark.json')], cwd=ROOT, env=env, check=True)
            provenance = {'commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(), 'modelSha256': hashlib.sha256(model.read_bytes()).hexdigest(), 'configSha256': hashlib.sha256((runtime / 'sharpai.json').read_bytes()).hexdigest(), 'dotnet': subprocess.check_output(['dotnet', '--version'], text=True).strip(), 'completed': True}
            (output / 'provenance.json').write_text(json.dumps(provenance, indent=2))
        finally:
            if server is not None and server.poll() is None:
                server.terminate()
                server.wait(timeout=15)
            if os.name == 'nt':
                subprocess.run(['taskkill', '/PID', str(sim.pid), '/T', '/F'], check=False, capture_output=True)
            else:
                try:
                    os.killpg(sim.pid, signal.SIGINT)
                except ProcessLookupError:
                    pass
            try:
                sim.wait(timeout=10)
            except subprocess.TimeoutExpired:
                if os.name != 'nt':
                    os.killpg(sim.pid, signal.SIGKILL)
                sim.wait(timeout=5)
            sim_log.close()
            sim_error.close()
            server_log.close()


if __name__ == '__main__':
    main()
