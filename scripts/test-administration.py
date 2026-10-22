#!/usr/bin/env python3
"""Run tenant/RBAC administration contracts against a private temporary server and SQLite database."""
import argparse
import json
import pathlib
import secrets
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--server-dll', default=str(ROOT / 'src/SharpAI.Server/bin/Debug/net10.0/SharpAI.Server.dll'))
    args = parser.parse_args()
    args.server_dll = str(pathlib.Path(args.server_dll).resolve())
    with tempfile.TemporaryDirectory(prefix='sharpai-administration-') as directory:
        runtime = pathlib.Path(directory)
        with socket.socket() as probe:
            probe.bind(('127.0.0.1', 0))
            port = probe.getsockname()[1]
        root = f'http://127.0.0.1:{port}'
        admin_key = secrets.token_hex(24)
        settings = {'Rest': {'Hostname': '127.0.0.1', 'Port': port}, 'Database': {'Type': 'Sqlite', 'Filename': './test.db'},
                    'Storage': {'ModelsDirectory': './models/', 'TempDirectory': './temp/'},
                    'Auth': {'Enabled': True, 'AdminApiKeys': [admin_key], 'TokenSigningKey': secrets.token_hex(32)},
                    'Telemetry': {'Enable': False}, 'RequestHistory': {'Enabled': True}, 'Runtime': {'ForceBackend': 'cpu'},
                    'Logging': {'LogDirectory': './logs/', 'ConsoleLogging': True}}
        (runtime / 'sharpai.json').write_text(json.dumps(settings))
        for name in ('models', 'temp', 'logs'):
            (runtime / name).mkdir()
        log = open(runtime / 'server.log', 'w')
        process = subprocess.Popen(['dotnet', args.server_dll], cwd=runtime, stdout=log, stderr=subprocess.STDOUT)
        checks = 0
        admin = {'x-api-key': admin_key}

        def check(condition, description):
            nonlocal checks
            if not condition:
                raise AssertionError(description)
            checks += 1
            print('PASS', description, flush=True)

        def request(method, path, body=None, headers=None, expected=200):
            payload = None if body is None else json.dumps(body).encode()
            req = urllib.request.Request(root + path, data=payload, method=method,
                                         headers={'Content-Type': 'application/json', **(admin if headers is None else headers)})
            try:
                with urllib.request.urlopen(req, timeout=20) as response:
                    status, data = response.status, json.load(response)
            except urllib.error.HTTPError as error:
                status, data = error.code, json.loads(error.read())
            check(status == expected, f'{method} {path.split("?")[0]} returns {expected} (actual {status}; {data.get("Message","") if isinstance(data,dict) and status != expected else ""})')
            return data

        def login(tenant, email, password):
            data = request('POST', '/v1.0/token', headers={'x-tenant-guid': tenant, 'x-email': email, 'x-password': password})
            return {'Authorization': 'Bearer ' + data['token']}, data['token']

        try:
            for _ in range(300):
                if process.poll() is not None:
                    raise RuntimeError('Private test server exited; production services were not touched.')
                try:
                    urllib.request.urlopen(root + '/health', timeout=1).close()
                    break
                except (urllib.error.URLError, TimeoutError):
                    time.sleep(.1)
            request('GET', '/v1.0/auth/context', headers={}, expected=401)
            context = request('GET', '/v1.0/auth/context')
            check(context['isAdmin'], 'API key receives platform administration context')
            a = request('POST', '/v1.0/tenants', {'Name': 'Tenant A'})['Guid']
            b = request('POST', '/v1.0/tenants', {'Name': 'Tenant B'})['Guid']
            base = '/v1.0/tenants/' + a
            other = '/v1.0/tenants/' + b
            password = 'Private test password ' + secrets.token_hex(8)
            ta = request('POST', base + '/users', {'Email': 'admin@example.test', 'Password': password, 'IsTenantAdmin': True})
            user = request('POST', base + '/users', {'Email': 'user@example.test', 'Password': password})
            foreign = request('POST', other + '/users', {'Email': 'user@example.test', 'Password': password})
            tenant_admin, _ = login(a, ta['Email'], password)
            user_auth, old_token = login(a, user['Email'], password)
            request('GET', '/v1.0/tenants', headers=tenant_admin, expected=403)
            request('POST', '/v1.0/tenants', {'Name': 'Escalated'}, tenant_admin, 403)
            request('POST', base + '/users', {'Email': 'escalate@example.test', 'Password': password, 'IsAdmin': True}, tenant_admin, 403)
            request('POST', base + '/users', {'Email': 'blank@example.test'}, expected=400)
            request('GET', other + '/users/' + foreign['Guid'], headers=tenant_admin, expected=403)
            request('PUT', base + '/users/' + foreign['Guid'], {'Email': 'changed@example.test'}, expected=404)
            request('GET', other + '/users/' + foreign['Guid'] + '/permissions', headers=tenant_admin, expected=403)
            request('GET', base + '/users/' + user['Guid'], headers=tenant_admin)
            check(not request('GET', base + '/users/' + user['Guid']).get('PasswordSha256'), 'User reads redact password hashes')
            roles = request('GET', base + '/roles')['Objects']
            builtin = next(role for role in roles if role['IsBuiltIn'])
            request('PUT', base + '/roles/' + builtin['Guid'], {'Name': 'Changed'}, expected=403)
            request('DELETE', base + '/roles/' + builtin['Guid'], expected=403)
            role = request('POST', base + '/roles', {'Name': 'Model reader'})
            foreign_role = request('POST', other + '/roles', {'Name': 'Foreign role'})
            permit = request('POST', base + '/permissions', {'Name': 'Read models', 'ResourceTypes': ['Model'], 'OperationTypes': ['Read'], 'Effect': 'Permit'})
            foreign_permission = request('POST', other + '/permissions', {'Name': 'Foreign', 'ResourceTypes': ['Model'], 'OperationTypes': ['Read'], 'Effect': 'Permit'})
            request('POST', base + '/roles/' + role['Guid'] + '/permissions/' + foreign_permission['Guid'], expected=404)
            request('POST', base + '/roles/' + role['Guid'] + '/permissions/' + permit['Guid'])
            mapped = request('GET', base + '/roles/' + role['Guid'] + '/permissions')
            check(any(item['Guid'] == permit['Guid'] for item in mapped), 'Custom role shows its permission mappings')
            request('POST', base + '/assignments', {'UserGuid': user['Guid'], 'RoleGuid': foreign_role['Guid']}, expected=400)
            request('POST', base + '/assignments', {'UserGuid': user['Guid'], 'RoleGuid': role['Guid'], 'ResourceScope': 'Resource'}, expected=400)
            assignment = request('POST', base + '/assignments', {'UserGuid': user['Guid'], 'RoleGuid': role['Guid'], 'ResourceScope': 'Tenant', 'InheritsToChildren': True})
            request('GET', '/v1/models', headers=user_auth)
            cred = request('POST', base + '/credentials', {'UserGuid': user['Guid'], 'Name': 'Integration'})
            credential_auth = {'x-access-key': cred['accessKey'], 'x-secret-key': cred['secretKey']}
            check(bool(cred['secretKey']), 'Creation returns a one-time secret')
            persisted = request('GET', base + '/credentials/' + cred['guid'])
            check(cred['secretKey'] not in json.dumps(persisted) and not persisted.get('SecretSha256'), 'Credential reads never return secrets or hashes')
            request('GET', '/v1/models', headers=credential_auth, expected=403)
            scope = request('POST', base + '/credentials/' + cred['guid'] + '/assignments', {'RoleGuid': role['Guid'], 'ResourceScope': 'Tenant', 'InheritsToChildren': True})
            request('GET', '/v1/models', headers=credential_auth)
            request('DELETE', base + '/assignments/' + assignment['Guid'])
            request('GET', '/v1/models', headers=credential_auth, expected=403)
            assignment = request('POST', base + '/assignments', {'UserGuid': user['Guid'], 'RoleGuid': role['Guid'], 'ResourceScope': 'Tenant'})
            deny = request('POST', base + '/permissions', {'Name': 'Deny models', 'ResourceTypes': ['Model'], 'OperationTypes': ['Read'], 'Effect': 'Deny'})
            request('POST', base + '/roles/' + role['Guid'] + '/permissions/' + deny['Guid'])
            request('GET', '/v1/models', headers=user_auth, expected=403)
            request('DELETE', base + '/roles/' + role['Guid'] + '/permissions/' + deny['Guid'])
            request('GET', '/v1/models', headers=user_auth)
            request('PUT', base + '/credentials/' + cred['guid'], {'Name': 'Disabled integration', 'Active': False})
            request('GET', '/v1/models', headers=credential_auth, expected=401)
            sessions = request('GET', base + '/sessions')['Objects']
            check(old_token not in json.dumps(sessions), 'Session inspection has no bearer token material')
            session = next(item for item in sessions if item['UserGuid'] == user['Guid'])
            request('DELETE', base + '/sessions/' + session['Guid'])
            request('GET', '/v1.0/auth/context', headers=user_auth, expected=401)
            user_auth, _ = login(a, user['Email'], password)
            new_password = 'Replacement ' + secrets.token_hex(12)
            request('PUT', base + '/users/' + user['Guid'] + '/password', {'Password': new_password})
            request('GET', '/v1.0/auth/context', headers=user_auth, expected=401)
            login(a, user['Email'], new_password)
            active_auth, _ = login(a, user['Email'], new_password)
            request('PUT', base + '/users/' + user['Guid'], {'Email': user['Email'], 'Active': False})
            request('GET', '/v1.0/auth/context', headers=active_auth, expected=401)
            request('POST', '/v1.0/token', headers={'x-tenant-guid': a, 'x-email': user['Email'], 'x-password': new_password}, expected=401)
            time.sleep(.2)
            history = request('GET', '/v1.0/api/request-history?pageSize=1000')['Objects']
            password_records = [item for item in history if item.get('Path', '').endswith('/password')]
            password_details = [request('GET', '/v1.0/api/request-history/' + item['Id']) for item in password_records]
            check(bool(password_details) and all('[redacted]' in str(item.get('RequestBody')) and new_password not in str(item.get('RequestBody')) for item in password_details), 'Password reset detail bodies are redacted in history')
            login_record = next(item for item in history if item.get('Path') == '/v1.0/token' and item.get('Method') == 'POST' and item.get('StatusCode') == 200)
            login_detail = request('GET', '/v1.0/api/request-history/' + login_record['Id'])
            check('[redacted]' in str(login_detail.get('ResponseBody')), 'Session-token response bodies are redacted before retention')
            token_record = next(item for item in history if item.get('Path') == '/v1.0/token')
            token_detail = request('GET', '/v1.0/api/request-history/' + token_record['Id'])
            check(any(key.lower() == 'x-password' and value == '[redacted]' for key, value in token_detail.get('RequestHeaders', {}).items()), 'Password authentication headers are redacted')
            audit = request('GET', '/v1.0/api/audit?tenantGuid=' + a)['Objects']
            check(any(item['EventType'] == 'PasswordReset' for item in audit), 'Password resets leave a security audit event')
            own_history = request('GET', '/v1.0/api/request-history?pageSize=1000', headers=tenant_admin)['Objects']
            check(bool(own_history) and all(item.get('TenantId') == a for item in own_history), 'Tenant history enumeration stays inside the authenticated tenant')
            request('GET', '/v1.0/api/request-history?tenantId=' + b, headers=tenant_admin, expected=403)
            request('GET', '/v1.0/api/request-history/summary?tenantId=' + b, headers=tenant_admin, expected=403)
            foreign_history = next(item for item in history if item.get('TenantId') != a)
            request('GET', '/v1.0/api/request-history/' + foreign_history['Id'], headers=tenant_admin, expected=403)
            request('DELETE', '/v1.0/api/request-history/' + foreign_history['Id'], headers=tenant_admin, expected=403)
            request('DELETE', base + '/credentials/' + cred['guid'] + '/assignments/' + scope['Guid'])
            request('DELETE', base + '/permissions/' + deny['Guid'])
            request('PUT', '/v1.0/tenants/' + a, {'Name': 'Tenant A', 'Active': False})
            request('GET', '/v1.0/auth/context', headers=tenant_admin, expected=401)
            request('POST', '/v1.0/token', headers={'x-tenant-guid': a, 'x-email': ta['Email'], 'x-password': password}, expected=401)
            print(f'{checks} isolated administration assertions passed', flush=True)
        finally:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
            log.close()


if __name__ == '__main__':
    main()
