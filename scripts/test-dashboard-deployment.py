#!/usr/bin/env python3
"""Read-only smoke checks through deployed nginx, including the API/SPA route boundary.

No model downloads or settings writes. POST /api/pull uses an empty request and must
reach server validation (400), rather than nginx static handling (405).
"""
import argparse
import json
import datetime
import urllib.parse
import re
import urllib.error
import urllib.request

parser = argparse.ArgumentParser()
parser.add_argument('--url', default='http://localhost:3000')
parser.add_argument('--engine-metrics', action='store_true', help='Require the full-stack collector metrics endpoint')
args = parser.parse_args()
base = args.url.rstrip('/')
checks = 0


def request(path, payload=None):
    req = urllib.request.Request(base + path, data=payload, headers={'Content-Type': 'application/json'} if payload else {})
    try:
        response = urllib.request.urlopen(req, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    return response.status, response.headers, response.read().decode(errors='replace')


def verify(condition, message):
    global checks
    assert condition, message
    checks += 1
    print('PASS', message)


for path in ['/health', '/ready', '/api/tags', '/api/ps', '/api/settings', '/v1/models', '/v1.0/api/request-history?pageSize=1', '/openapi.json']:
    status, headers, body = request(path)
    verify(status == 200 and 'application/json' in headers.get('Content-Type', ''), path + ' returns real JSON through nginx')
    value = json.loads(body)
    verify(isinstance(value, dict), path + ' is an object, not HTML')
    if path == '/api/settings':
        verify(all(key in value for key in ['Storage', 'Runtime', 'Database']), 'settings contains server sections')
    if path == '/api/tags':
        verify(isinstance(value.get('models'), list), 'model inventory has the expected shape')
# Browser encodes colons and slashes; both endpoints must honor the same window.
now = datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(seconds=2)
values = {'fromUtc': (now - datetime.timedelta(minutes=2)).isoformat(), 'toUtc': now.isoformat(), 'pathContains': '/health', 'pageSize': 1}
encoded = urllib.parse.urlencode(values)
summary = json.loads(request('/v1.0/api/request-history/summary?' + encoded)[2])
page = json.loads(request('/v1.0/api/request-history?' + encoded)[2])
verify(len(summary['Buckets']) == 1, 'encoded timestamps constrain the summary to two minutes')
verify(summary['TotalCount'] == page['TotalRecords'], 'summary and table honor identical encoded filters')
verify(all('/health' in item['Path'] for item in page['Objects']), 'encoded path filter matches request paths')
status, headers, body = request('/api/pull', b'{}')
verify(status == 400 and 'application/json' in headers.get('Content-Type', ''), 'pull POST reaches server validation; no nginx 405')
for route in ['/settings', '/models', '/api-explorer', '/observability']:
    status, headers, body = request(route)
    verify(status == 200 and '<div id="root">' in body, route + ' remains an SPA route')
status, headers, body = request('/')
for asset in re.findall(r'(?:src|href)="([^"]+)"', body):
    if asset.startswith('/'):
        verify(request(asset)[0] == 200, asset + ' loads')
if args.engine_metrics:
    status, headers, body = request('/engine-metrics')
    verify(status == 200 and 'sharpai_models_resident' in body, 'engine metrics reach the dashboard from the OTLP collector')
print(f'{checks} deployment assertions passed')
