#!/usr/bin/env python3
"""Validate log schemas and levels against real native standalone inference."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--framework', choices=['net8.0', 'net10.0'], default='net8.0')
    parser.add_argument('--command', type=Path)
    args = parser.parse_args()
    if args.command:
        command = [str(args.command.resolve())]
    else:
        subprocess.run(['dotnet', 'build', str(ROOT / 'src/SharpAI.Serve'), '-f', args.framework, '-v', 'quiet'], check=True)
        command = ['dotnet', str(ROOT / 'src/SharpAI.Serve/bin/Debug' / args.framework / 'SharpAI.Serve.dll')]
    assertions = 0

    def check(condition, message):
        nonlocal assertions
        assert condition, message
        assertions += 1

    scenarios = [dict(format='json', detail=depth, quiet=False, level='debug') for depth in ['off', 'minimal', 'standard', 'detailed']]
    scenarios.extend([dict(format='text', detail='detailed', quiet=False, level='debug'), dict(format='text', detail='standard', quiet=True, level='debug'), dict(format='json', detail='detailed', quiet=True, level='debug'), dict(format='json', detail='detailed', quiet=False, level='info')])
    for scenario in scenarios:
        with tempfile.TemporaryDirectory(prefix='sharpai-log-') as temporary:
            directory = Path(temporary)
            with socket.socket() as listener:
                listener.bind(('127.0.0.1', 0))
                port = listener.getsockname()[1]
            base = 'http://127.0.0.1:' + str(port)
            with (directory / 'stdout').open('w+') as output, (directory / 'stderr').open('w+') as errors:
                options = ['--log-format', scenario['format'], '--log-detail', scenario['detail'], '--log-level', scenario['level']]
                if scenario['quiet']:
                    options += ['--quiet', '--log-file', str(directory / 'app.log')]
                process = subprocess.Popen(command + ['serve', str(args.model.resolve()), '--served-model-name', 'fixture', '--backend', 'cpu', '--context-size', '512', '--max-tokens', '8', '--threads', '2', '--parallel', '2', '--port', str(port)] + options, cwd=directory, stdout=output, stderr=errors, creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0)
                def request(path, body=None, status=200):
                    http = urllib.request.Request(base + path, data=None if body is None else json.dumps(body).encode(), headers={'Content-Type': 'application/json', 'Authorization': 'Bearer NEVER_LOG_TOKEN'})
                    try:
                        response = urllib.request.urlopen(http, timeout=30)
                    except urllib.error.HTTPError as error:
                        response = error
                    with response:
                        check(response.code == status, 'expected HTTP status')
                        return dict(body=response.read().decode(), request_id=response.headers['X-SharpAI-Request'])
                try:
                    for attempt in range(120):
                        if process.poll() is not None:
                            errors.seek(0)
                            raise RuntimeError(errors.read())
                        try:
                            urllib.request.urlopen(base + '/ready', timeout=1).close()
                            break
                        except OSError:
                            time.sleep(.1)
                    else:
                        raise AssertionError('readiness timeout')
                    request('/health?private=NEVER_LOG_QUERY')
                    body = {'model': 'fixture', 'messages': [{'role': 'user', 'content': 'NEVER_LOG_PROMPT'}], 'max_tokens': 4}
                    normal = request('/v1/chat/completions', body)
                    stream = request('/v1/chat/completions', dict(body, stream=True, stream_options={'include_usage': True}))
                    failure = request('/v1/chat/completions', dict(body, model='missing'), status=404)
                    peers = []
                    if scenario['format'] == 'json' and scenario['detail'] == 'detailed' and scenario['level'] == 'debug':
                        with ThreadPoolExecutor(max_workers=2) as pool:
                            pending = [pool.submit(request, '/v1/chat/completions', body) for index in range(2)]
                            peers = [future.result() for future in pending]

                    time.sleep(.05)  # PostRouting runs after the final response write.
                finally:
                    if os.name == 'nt':
                        process.send_signal(signal.CTRL_BREAK_EVENT)
                    else:
                        process.terminate()
                    process.wait(timeout=30)
                check(process.returncode == 0, 'graceful shutdown')
                output.seek(0)
                transcript = output.read()
                if scenario['quiet']:
                    check(transcript == '', 'quiet stdout is empty')
                    transcript = (directory / 'app.log').read_text()
                for secret in ['NEVER_LOG_QUERY', 'NEVER_LOG_TOKEN', 'NEVER_LOG_PROMPT']:
                    check(secret not in transcript, 'private data is excluded')
                if scenario['format'] == 'text':
                    if scenario['detail'] == 'detailed':
                        check('queue_ms=' in transcript and 'generation_tokens_per_second=' in transcript, 'detailed text metrics')
                    check('{"' not in transcript, 'text console and file output contain no JSON envelopes')
                    check('[NativeBackendInitializer]' in transcript, 'shared library message is readable text')
                    check('engine.inference.completed' in transcript and 'prompt_tokens=' in transcript, 'engine statistics survive text rendering')
                    continue
                records = [json.loads(line) for line in transcript.splitlines()]
                check(all(isinstance(record, dict) and record['schema_version'] == 1 and record['severity'] for record in records), 'every stdout/file line is a full compact log object')
                completed = [record for record in records if record['event'] == 'request.completed']
                started = [record for record in records if record['event'] == 'request.started']
                if scenario['detail'] == 'off' or scenario['level'] == 'info':
                    check(not completed and not started, 'request logs filtered')
                    continue
                check(bool(started) == (scenario['detail'] != 'minimal'), 'minimal omits processing events')
                normal_record = next(record for record in completed if record['request_id'] == normal['request_id'])
                stream_record = next(record for record in completed if record['request_id'] == stream['request_id'])
                failed_record = next(record for record in completed if record['request_id'] == failure['request_id'])
                check(failed_record['status'] == 404 and normal_record['status'] == 200, 'status and response-header correlation')
                check(all(record['duration_ms'] >= 0 for record in completed), 'nonnegative HTTP durations')
                if scenario['detail'] == 'minimal':
                    check('prompt_tokens' not in normal_record and 'backend' not in normal_record, 'minimal excludes inference detail')
                    continue
                usage = json.loads(normal['body'])['usage']
                check(normal_record['prompt_tokens'] == usage['prompt_tokens'] and normal_record['completion_tokens'] == usage['completion_tokens'], 'usage agrees with HTTP response')
                check(normal_record['statistics_available'] and normal_record['ttft_ms'] > 0, 'measured statistics')
                check(not failed_record['statistics_available'] and failed_record['ttft_ms'] is None, 'unavailable statistics are null')
                if scenario['detail'] != 'detailed':
                    check('queue_ms' not in normal_record, 'standard excludes phase detail')
                    continue
                events = [line[6:] for line in stream['body'].splitlines() if line.startswith('data: ')]
                check(stream_record['stream_chunks'] == len(events), 'stream write count agrees with SSE events')
                check(stream_record['stream_payload_bytes'] == sum(len(event.encode('utf-8')) for event in events), 'stream byte count agrees with SSE data payloads')
                check(stream_record['first_stream_write_ms'] >= 0 and stream_record['stream_duration_ms'] >= 0, 'stream write timings')
                check(stream_record['response_content_length'] is None and normal_record['response_content_length'] > 0, 'declared length is separate from stream payload bytes')
                check(normal_record['response_content_length'] == len(normal['body'].encode('utf-8')), 'nonstreaming response length matches actual body')
                check(normal_record['backend'] == 'cpu' and normal_record['context_size'] == 512, 'effective runtime configuration')
                check(normal_record['engine_timing_available'] and normal_record['timing_source'] in ['native', 'backend_step_wall_clock'], 'timing provenance')
                check(normal_record['generation_tokens_per_second'] > 0 and normal_record['prompt_tokens_per_second'] > 0, 'measured throughput')
                check(abs(normal_record['generation_tokens_per_second'] - usage['completion_tokens'] * 1000 / normal_record['generation_ms']) < .001, 'throughput uses generation phase')
                for field in ['queue_ms', 'load_ms', 'prompt_eval_ms', 'generation_ms', 'engine_total_ms']:
                    check(normal_record[field] >= 0, field)

                check(len({record['request_id'] for record in completed}) == len(completed), 'completion IDs are unique')
                for peer in peers:
                    matched = next(record for record in completed if record['request_id'] == peer['request_id'])
                    peer_usage = json.loads(peer['body'])['usage']
                    check(matched['prompt_tokens'] == peer_usage['prompt_tokens'] and matched['completion_tokens'] == peer_usage['completion_tokens'], 'concurrent statistics remain request-scoped')
                for record in started:
                    check(any(end['request_id'] == record['request_id'] for end in completed), 'start/end correlation')
    with tempfile.TemporaryDirectory(prefix='sharpai-log-errors-') as temporary:
        missing = str(Path(temporary) / 'missing.gguf')
        result = subprocess.run(command + ['serve', missing, '--log-format=json'], capture_output=True, text=True, timeout=30)
        check(result.returncode == 2 and json.loads(result.stderr)['event'] == 'startup.failed', 'argument errors follow JSON format')
        check(result.stdout == '', 'argument errors stay on stderr')
    print('PASS: ' + str(assertions) + ' log schema, privacy, detail and format checks (' + args.framework + ')')


if __name__ == '__main__':
    main()
