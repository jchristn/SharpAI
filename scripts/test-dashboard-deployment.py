#!/usr/bin/env python3
"""Read-only smoke checks through deployed nginx, including the API/SPA route boundary.

No model downloads or settings writes. POST /api/pull uses an empty request and must
reach server validation (400), rather than nginx static handling (405).
"""
import argparse
import json
import os
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
bearer_token = os.environ.get("SHARPAI_BEARER_TOKEN", "")


def request(path, payload=None, headers=None, anonymous=False):
    req = urllib.request.Request(base + path, data=payload, headers={**({'Authorization': 'Bearer ' + bearer_token} if bearer_token and not anonymous else {}), **({'Content-Type': 'application/json'} if payload else {}), **(headers or {})})
    try:
        response = urllib.request.urlopen(req, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    return {'status': response.status, 'headers': response.headers, 'body': response.read().decode(errors='replace')}


def verify(condition, message):
    global checks
    assert condition, message
    checks += 1
    print('PASS', message)


for protected in ['/api/settings','/api/tags','/v1/models','/openapi.json','/metrics'] + (['/engine-metrics'] if args.engine_metrics else []):
    verify(request(protected, anonymous=True)['status'] == 401, protected + ' rejects anonymous access')
if not bearer_token:
    login = request('/v1.0/token', b'', headers={
        'x-email': os.environ.get('SHARPAI_EMAIL', 'admin@sharpai.local'),
        'x-password': os.environ.get('SHARPAI_PASSWORD', 'password'),
        'x-tenant-guid': os.environ.get('SHARPAI_TENANT', 'default'),
    }, anonymous=True)
    verify(login['status'] == 200, 'administrator signs in for deployment checks')
    bearer_token = json.loads(login['body'])['token']

for path in ['/health', '/ready', '/api/tags', '/api/ps', '/api/settings', '/v1/models', '/v1.0/api/request-history?pageSize=1', '/openapi.json']:
    response = request(path)
    status = response['status']
    headers = response['headers']
    body = response['body']
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
summary = json.loads(request('/v1.0/api/request-history/summary?' + encoded)['body'])
page = json.loads(request('/v1.0/api/request-history?' + encoded)['body'])
verify(len(summary['Buckets']) == 1, 'encoded timestamps constrain the summary to two minutes')
verify(summary['TotalCount'] == page['TotalRecords'], 'summary and table honor identical encoded filters')
verify(all('/health' in item['Path'] for item in page['Objects']), 'encoded path filter matches request paths')
response = request('/api/pull', b'{}')
status = response['status']
headers = response['headers']
body = response['body']
verify(status == 400 and 'application/json' in headers.get('Content-Type', ''), 'pull POST reaches server validation; no nginx 405')
for route in ['/settings', '/models', '/api-explorer', '/observability']:
    response = request(route)
    status = response['status']
    headers = response['headers']
    body = response['body']
    verify(status == 200 and '<div id="root">' in body, route + ' remains an SPA route')
response = request('/')
status = response['status']
headers = response['headers']
body = response['body']
for asset in re.findall(r'(?:src|href)="([^"]+)"', body):
    if asset.startswith('/'):
        verify(request(asset)['status'] == 200, asset + ' loads')
if args.engine_metrics:
    response = request('/engine-metrics')
    status = response['status']
    headers = response['headers']
    body = response['body']
    verify(status == 200 and 'sharpai_models_resident' in body, 'engine metrics reach the dashboard from the OTLP collector')
print(f'{checks} deployment assertions passed')
