#!/usr/bin/env python3
"""Run the real standalone server from an unrelated directory; no mocks or auth."""
import argparse
import json
import os
from pathlib import Path
import socket
import signal
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', required=True, type=Path)
    parser.add_argument('--embedding-model', type=Path)
    parser.add_argument('--framework', default='net8.0', choices=['net8.0', 'net10.0'])
    parser.add_argument('--postman', action='store_true', help='Also run applicable saved standalone collection requests')
    parser.add_argument('--command', help='Installed sharpai executable (otherwise build and run DLL)')
    args = parser.parse_args()
    if args.command:
        command = [str(Path(args.command).resolve())]
    else:
        subprocess.run(['dotnet', 'build', str(ROOT / 'src/SharpAI.Serve'), '-f', args.framework, '-v', 'quiet'], check=True)
        command = ['dotnet', str(ROOT / 'src/SharpAI.Serve/bin/Debug' / args.framework / 'SharpAI.Serve.dll')]
    assertions = 0

    def check(condition, message):
        nonlocal assertions
        assert condition, message
        assertions += 1

    with tempfile.TemporaryDirectory(prefix='sharpai-serve-') as temporary:
        directory = Path(temporary)
        for arguments, expected in [(['/?'], 0), (['--help'], 0), (['usage'], 0), (['--version'], 0), (['serve'], 2), (['serve', str(directory / 'missing.gguf')], 2), (['serve', str(args.model.resolve()), '--port', '0'], 2)]:
            result = subprocess.run(command + arguments, cwd=directory, capture_output=True, text=True, timeout=30)
            check(result.returncode == expected, str(arguments) + result.stderr)
        invalid = directory / 'invalid.gguf'
        invalid.write_text('not a GGUF')
        result = subprocess.run(command + ['serve', str(invalid), '--backend', 'cpu'], cwd=directory, capture_output=True, text=True, timeout=30)
        check(result.returncode == 1, 'bad GGUF fails startup')
        invalid.unlink()
        template = directory / 'template.txt'
        template.write_text('')
        result = subprocess.run(command + ['serve', str(args.model.resolve()), '--backend', 'cpu', '--context-size', '512', '--threads', '2', '--chat-template', str(template)], cwd=directory, capture_output=True, text=True, timeout=30)
        check(result.returncode == 1, 'empty explicit template fails startup')
        template.unlink()


        models = [{'path': args.model.resolve(), 'embedding': False, 'api': 'both'}]
        if args.embedding_model:
            models.append({'path': args.embedding_model.resolve(), 'embedding': True, 'api': 'openai'})
        models.append({'path': args.model.resolve(), 'embedding': False, 'api': 'ollama'})
        for fixture in models:
            with socket.socket() as listener:
                listener.bind(('127.0.0.1', 0))
                port = listener.getsockname()[1]
            base = 'http://127.0.0.1:' + str(port)
            logfile = directory / 'process.log'
            with logfile.open('w+') as output:
                process = subprocess.Popen(command + ['serve', str(fixture['path']), '--served-model-name', 'fixture', '--backend', 'cpu', '--port', str(port), '--context-size', '512', '--max-tokens', '16', '--threads', '2', '--api', fixture['api']], cwd=directory, stdout=output, stderr=output, creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0)
                def request(path, body=None, expected=200, raw=False):
                    payload = None if body is None else json.dumps(body).encode()
                    http = urllib.request.Request(base + path, data=payload, headers={'Content-Type': 'application/json'})
                    try:
                        response = urllib.request.urlopen(http, timeout=60)
                    except urllib.error.HTTPError as error:
                        response = error
                    with response:
                        data = response.read().decode()
                        check(response.code == expected, path + ': ' + str(response.code) + ' ' + data)
                        return data if raw else json.loads(data)
                try:
                    ready = False
                    for attempt in range(120):
                        if process.poll() is not None:
                            output.seek(0)
                            raise RuntimeError(output.read())
                        try:
                            with urllib.request.urlopen(base + '/ready', timeout=1) as response:
                                ready = json.load(response)['model'] == 'fixture'
                            break
                        except OSError:
                            time.sleep(.25)
                    check(ready, 'ready after model load')
                    request('/health')
                    # A second process must fail instead of advertising false readiness on an occupied port.
                    if fixture['api'] == 'both':
                        occupied = subprocess.run(command + ['serve', str(fixture['path']), '--backend', 'cpu', '--port', str(port), '--context-size', '512', '--threads', '2'], cwd=directory, capture_output=True, text=True, timeout=30)
                        check(occupied.returncode == 1, 'occupied port fails startup')
                        request('/v1/completions', {'model': 'fixture', 'prompt': 'Hello', 'max_tokens': 'invalid'}, expected=400)
                        request('/v1/chat/completions', {'model': 'fixture', 'messages': [{'role': 'user', 'content': 'Hello'}], 'stream': 'invalid'}, expected=400)

                    request('/v1.0/models', expected=404)
                    request('/dashboard', expected=404)
                    request('/api/pull', {'model': 'fixture'}, expected=404)
                    if args.postman:
                        collection = ROOT / 'postman/SharpAI.Serve.postman_collection.json'
                        saved = json.loads(collection.read_text())
                        selected = []
                        for folder in saved['item']:
                            if folder['name'] == 'Ollama APIs' and fixture['api'] == 'openai' or folder['name'] == 'OpenAI APIs' and fixture['api'] == 'ollama':
                                continue
                            for item in folder['item']:
                                name = item['name']
                                if '/embed' in name and not fixture['embedding']:
                                    continue
                                if any(route in name for route in ['/chat', '/generate', '/completions']) and fixture['embedding']:
                                    continue
                                selected.extend(['-i', item['id']])
                        subprocess.run(['postman', 'collection', 'run', str(collection), '--env-var', 'baseUrl=' + base, '--env-var', 'model=fixture', '--no-report-events'] + selected, check=True, stdout=subprocess.DEVNULL)
                        check(True, 'saved Postman requests')

                    if fixture['api'] != 'ollama':
                        check(request('/v1/models')['data'][0]['id'] == 'fixture', 'one OpenAI model')
                        route = '/v1/embeddings' if fixture['embedding'] else '/v1/completions'
                        body = {'model': 'wrong', 'input': 'Hello'} if fixture['embedding'] else {'model': 'wrong', 'prompt': 'Hello'}
                        request(route, body, expected=404)
                        request('/v1/chat/completions', {'model': 'fixture', 'messages': []}, expected=400)
                        if fixture['embedding']:
                            data = request('/v1/embeddings', {'model': 'fixture', 'input': ['Hello', 'World']})
                            check(len(data['data']) == 2 and len(data['data'][0]['embedding']) == 384, 'real embeddings')
                            request('/v1/completions', {'model': 'fixture', 'prompt': 'Hello'}, expected=403)
                            request('/api/tags', expected=404)
                        else:
                            data = request('/v1/completions', {'model': 'fixture', 'prompt': 'Once upon a time', 'max_tokens': 4})
                            check(0 < data['usage']['completion_tokens'] <= 4, 'request budget override')
                            data = request('/v1/chat/completions', {'model': 'fixture', 'messages': [{'role': 'user', 'content': 'Hello'}]})
                            check(0 < data['usage']['completion_tokens'] <= 16, 'CLI output budget')
                            stream = request('/v1/chat/completions', {'model': 'fixture', 'messages': [{'role': 'user', 'content': 'Hello'}], 'stream': True, 'stream_options': {'include_usage': True}}, raw=True)
                            check('data: [DONE]' in stream, 'SSE terminal sentinel')
                            check('completion_tokens' in stream and 'finish_reason' in stream, 'SSE usage and final chunk')
                            request('/v1/completions', {'model': 'fixture', 'prompt': 'hello ' * 1000}, expected=400)
                    else:
                        request('/v1/models', expected=404)
                    if fixture['api'] != 'openai':
                        check(len(request('/api/tags')['models']) == 1, 'one Ollama model')
                        request('/api/show', {'model': 'fixture'})
                        request('/api/ps')
                        request('/api/version')
                        data = request('/api/generate', {'model': 'fixture', 'prompt': 'Hello', 'stream': False})
                        check(data['done'] and data['eval_count'] <= 16, 'Ollama nonstreaming budget')
                        stream = request('/api/chat', {'model': 'fixture', 'messages': [{'role': 'user', 'content': 'Hello'}]}, raw=True)
                        chunks = [json.loads(line) for line in stream.splitlines() if line]
                        check(chunks[-1]['done'], 'Ollama defaults to streaming and includes final chunk')
                        check(chunks[-1]['eval_count'] <= 16, 'Ollama streaming budget')
                finally:
                    if os.name == "nt":
                        process.send_signal(signal.CTRL_BREAK_EVENT)
                    else:
                        process.terminate()
                    try:
                        process.wait(timeout=30)
                    except subprocess.TimeoutExpired:
                        process.kill()
                        process.wait()
                        raise AssertionError('server did not shut down gracefully')
                    check(process.returncode == 0, 'graceful shutdown: ' + str(process.returncode))
                    output.seek(0)
                    if process.returncode:
                        print(output.read())
            logfile.unlink()
        check(list(directory.iterdir()) == [], 'no settings, registry, model copies or log files created')
    print('PASS: ' + str(assertions) + ' standalone live assertions (' + args.framework + ')')


if __name__ == '__main__':
    main()
