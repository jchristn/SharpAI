#!/usr/bin/env python3
"""Live chat compatibility checks against an already-installed model (no downloads)."""
import argparse
import json
import os
import urllib.request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-url', default='http://localhost:3000')
    parser.add_argument('--model', required=True)
    args = parser.parse_args()
    checks = 0

    def check(condition, description):
        nonlocal checks
        if not condition:
            raise AssertionError(description)
        checks += 1
        print('PASS', description)

    for protocol in ['ollama', 'openai']:
        for stream in [False, True]:
            body = {'model': args.model, 'messages': [{'role': 'user', 'content': 'Reply with only OK. /no_think'}], 'stream': stream}
            options = {'temperature': 0, 'top_p': 0.9, 'presence_penalty': 0.1, 'frequency_penalty': 0.1, 'seed': 42}
            if protocol == 'ollama':
                path = '/api/chat'
                body['options'] = {**options, 'num_predict': 32}
            else:
                path = '/v1/chat/completions'
                body.update(options)
                body['max_tokens'] = 32
                if stream:
                    body['stream_options'] = {'include_usage': True}
            headers = {'Content-Type': 'application/json'}
            if os.environ.get('SHARPAI_API_KEY'):
                headers['x-api-key'] = os.environ['SHARPAI_API_KEY']
            req = urllib.request.Request(args.base_url.rstrip('/') + path, data=json.dumps(body).encode(), headers=headers)
            with urllib.request.urlopen(req, timeout=180) as response:
                check(response.status == 200, f'{protocol} stream={stream} HTTP success')
                payload = response.read().decode()
            if not stream:
                events = [json.loads(payload)]
            elif protocol == 'ollama':
                events = [json.loads(line) for line in payload.splitlines() if line.strip()]
            else:
                check('data: [DONE]' in payload, 'OpenAI SSE terminates with [DONE]')
                events = [json.loads(line[5:].strip()) for line in payload.splitlines() if line.startswith('data:') and line[5:].strip() != '[DONE]']
            check(all(not event.get('error') for event in events), f'{protocol} stream={stream} has no protocol errors')
            if protocol == 'ollama':
                check(events[-1].get('done') is True, f'Ollama stream={stream} completion marker')
                text = ''.join(event.get('message', {}).get('content', '') for event in events)
                usage = events[-1]
                check(usage.get('prompt_eval_count', 0) > 0 and usage.get('eval_count', 0) > 0, f'Ollama stream={stream} actual input/output counts')
            else:
                text = ''.join(choice.get('delta' if stream else 'message', {}).get('content') or '' for event in events for choice in event.get('choices', []))
                usage = next((event['usage'] for event in reversed(events) if event.get('usage')), {})
                check(usage.get('prompt_tokens', 0) > 0 and usage.get('completion_tokens', 0) > 0, f'OpenAI stream={stream} actual input/output counts')
                check(usage.get('total_tokens') == usage.get('prompt_tokens', 0) + usage.get('completion_tokens', 0), f'OpenAI stream={stream} total reconciles')
            check(bool(text.strip()), f'{protocol} stream={stream} emits model text')
    print(f'{checks} live chat assertions passed')


if __name__ == '__main__':
    main()
