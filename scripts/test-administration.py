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
            request('GET', '/v1.0/system-prompts', headers={}, expected=401)
            context = request('GET', '/v1.0/auth/context')
            check(context['isAdmin'], 'API key receives platform administration context')
            seeded, _ = login('default', 'admin@sharpai.local', 'password')
            seeded_context = request('GET', '/v1.0/auth/context', headers=seeded)
            check(seeded_context['isAdmin'], 'initial password signs in to the default tenant as platform administrator')
            default_tenant = request('GET', '/v1.0/tenants/default')
            check(default_tenant['Guid'] == 'default' and default_tenant['IsProtected'], 'seeded tenant has stable protected default ID')
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
            # Library ownership is intrinsic to human users, with explicit RBAC denies still honored.
            library_ids={}
            for library, payload in [('system-prompts', {'Content': 'Shared starter instructions'}), ('model-configurations', {'Configuration': {'Temperature': .7, 'MaxTokens': 256, 'TopP': .8, 'TopK': 30, 'MinP': .1, 'RepeatPenalty': 1.1, 'RepeatLastN': 32, 'PresencePenalty': .2, 'FrequencyPenalty': -.1, 'Seed': 9, 'Stop': ['END'], 'StreamResponse': False, 'JsonOutput': True, 'DisplayThinking': True}})]:
                endpoint='/v1.0/'+library
                system=request('POST',endpoint,{'Name':'Shared system','Scope':'System',**payload})
                tenant_item=request('POST',endpoint,{'Name':'Shared tenant','Scope':'Tenant','TenantId':a,**payload},tenant_admin)
                library_ids[library]=tenant_item['Id']
                request('PUT',endpoint+'/'+tenant_item['Id'],{'Name':'Rescope','Scope':'User',**payload},tenant_admin,400)
                other_tenant=request('POST',endpoint,{'Name':'Other tenant','Scope':'Tenant','TenantId':b,**payload})
                private=request('POST',endpoint,{'Name':'My item','Scope':'User','TenantId':b,**payload},user_auth)
                check(private['TenantId']==a and private['UserId']==user['Guid'],'Personal ownership is derived from the caller, not submitted tenant')
                admin_private=request('POST',endpoint,{'Name':'Other user private','Scope':'User',**payload},tenant_admin)
                visible=request('GET',endpoint,headers=user_auth)
                visible_ids={item['Id'] for item in visible['Objects']}
                check({system['Id'],tenant_item['Id'],private['Id']}<=visible_ids and not {other_tenant['Id'],admin_private['Id']}&visible_ids,'System and tenant defaults plus own private items are visible, without leaking another tenant or user')
                check(next(item for item in visible['Objects'] if item['Id']==private['Id'])['CanEdit'] and not next(item for item in visible['Objects'] if item['Id']==system['Id'])['CanEdit'],'Edit capabilities follow item ownership')
                request('GET',endpoint+'/'+admin_private['Id'],headers=user_auth,expected=404)
                request('GET',endpoint+'/'+private['Id'],headers=tenant_admin,expected=404)
                request('GET',endpoint+'?tenantId='+b,headers=user_auth,expected=403)
                request('PUT',endpoint+'/'+system['Id'],{'Name':'Overwrite','Scope':'System',**payload},user_auth,403)
                request('DELETE',endpoint+'/'+system['Id'],headers=user_auth,expected=403)
                request('POST',endpoint,{'Name':'Escalated','Scope':'System',**payload},tenant_admin,403)
                request('POST',endpoint,{'Name':'Escalated','Scope':'Tenant','TenantId':b,**payload},tenant_admin,403)
                request('POST',endpoint,{'Name':'Escalated','Scope':'Tenant','TenantId':a,**payload},user_auth,403)
                request('POST',endpoint,{'Name':'Forged owner','Scope':'User','UserId':ta['Guid'],**payload},user_auth,403)
                copy=request('POST',endpoint+'/'+system['Id']+'/duplicate',{'Name':'Personal copy'},user_auth)
                check(copy['Scope']=='User' and copy['UserId']==user['Guid'],'Duplicate defaults to the current user scope')
                changed={**payload, 'Content':'Changed instructions'} if library=='system-prompts' else {'Configuration':{**payload['Configuration'],'Temperature':.2}}
                edited=request('PUT',endpoint+'/'+copy['Id'],{'Name':'Edited copy','Scope':'User',**changed},user_auth)
                unchanged=request('GET',endpoint+'/'+system['Id'],headers=user_auth)
                check((edited['Content']!=unchanged['Content']) if library=='system-prompts' else edited['Configuration']['Temperature']!=unchanged['Configuration']['Temperature'],'Editing a duplicate leaves the shared source unchanged')
                pages=[request('GET',endpoint+'?pageSize=1&pageNumber='+str(i),headers=user_auth)['Objects'][0]['Id'] for i in range(1,int(visible['TotalRecords'])+2)]
                check(len(pages)==len(set(pages)) and private['Id'] in pages and admin_private['Id'] not in pages,'Pagination filters ownership before count and page boundaries')
                request('POST',endpoint,{'Name':'No owner','Scope':'User',**payload},expected=400)
                if library=='model-configurations':
                    request('POST',endpoint,{'Name':'Invalid sampling','Scope':'User','Configuration':{'Temperature':3}},user_auth,400)
                request('DELETE',endpoint+'/'+copy['Id'],headers=user_auth)
                request('GET',endpoint+'/'+copy['Id'],headers=user_auth,expected=404)

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
            library_deny=request('POST',base+'/permissions',{'Name':'Deny prompt reads','ResourceTypes':['SystemPrompt'],'OperationTypes':['Read'],'Effect':'Deny'})
            request('POST',base+'/roles/'+role['Guid']+'/permissions/'+library_deny['Guid'])
            mapped = request('GET', base + '/roles/' + role['Guid'] + '/permissions')
            check(any(item['Guid'] == permit['Guid'] for item in mapped), 'Custom role shows its permission mappings')
            request('POST', base + '/assignments', {'UserGuid': user['Guid'], 'RoleGuid': foreign_role['Guid']}, expected=400)
            request('POST', base + '/assignments', {'UserGuid': user['Guid'], 'RoleGuid': role['Guid'], 'ResourceScope': 'Resource'}, expected=400)
            assignment = request('POST', base + '/assignments', {'UserGuid': user['Guid'], 'RoleGuid': role['Guid'], 'ResourceScope': 'Tenant', 'InheritsToChildren': True})
            request('GET', '/v1/models', headers=user_auth)
            request('GET','/v1.0/system-prompts',headers=user_auth,expected=403)
            request('GET','/v1.0/model-configurations',headers=user_auth)
            request('DELETE',base+'/roles/'+role['Guid']+'/permissions/'+library_deny['Guid'])
            scoped_role=request('POST',base+'/roles',{'Name':'Deny one prompt'})
            request('POST',base+'/roles/'+scoped_role['Guid']+'/permissions/'+library_deny['Guid'])
            before_scope=request('GET','/v1.0/system-prompts',headers=user_auth)
            scoped_assignment=request('POST',base+'/assignments',{'UserGuid':user['Guid'],'RoleGuid':scoped_role['Guid'],'ResourceScope':'Resource','ResourceGuid':library_ids['system-prompts']})
            after_scope=request('GET','/v1.0/system-prompts?pageSize=1000',headers=user_auth)
            check(int(after_scope['TotalRecords'])==int(before_scope['TotalRecords'])-1 and library_ids['system-prompts'] not in {row['Id'] for row in after_scope['Objects']},'Resource-specific read denials filter both list contents and totals')
            request('GET','/v1.0/system-prompts/'+library_ids['system-prompts'],headers=user_auth,expected=403)
            request('DELETE',base+'/assignments/'+scoped_assignment['Guid'])

            cred = request('POST', base + '/credentials', {'UserGuid': user['Guid'], 'Name': 'Integration'})
            credential_auth = {'x-access-key': cred['accessKey'], 'x-secret-key': cred['secretKey']}
            check(bool(cred['secretKey']), 'Creation returns a one-time secret')
            persisted = request('GET', base + '/credentials/' + cred['guid'])
            check(cred['secretKey'] not in json.dumps(persisted) and not persisted.get('SecretSha256'), 'Credential reads never return secrets or hashes')
            request('GET', '/v1/models', headers=credential_auth, expected=403)
            request('GET', '/v1.0/system-prompts', headers=credential_auth, expected=403)
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
            library_history = next(item for item in history if item['Method']=='POST' and item['Path']=='/v1.0/system-prompts')
            library_detail=request('GET','/v1.0/api/request-history/'+library_history['Id'])
            check(library_detail.get('RequestBody')=='[private library payload withheld]','Shared request history cannot expose private library payloads')
            # Open inference mode still resolves explicit identities for private libraries.
            foreign_auth, _ = login(b, foreign['Email'], password)
            settings['Auth']['Enabled'] = False
            settings['Debug'] = {'RequestBody': True}
            (runtime / 'sharpai.json').write_text(json.dumps(settings))
            process.terminate()
            process.wait(timeout=10)
            process = subprocess.Popen(['dotnet', args.server_dll], cwd=runtime, stdout=log, stderr=subprocess.STDOUT)
            for _ in range(300):
                if process.poll() is not None: raise RuntimeError('Private open-mode server exited')
                try:
                    urllib.request.urlopen(root + '/health', timeout=1).close()
                    break
                except (urllib.error.URLError, TimeoutError): time.sleep(.1)
            open_context=request('GET','/v1.0/library-context',headers={})
            check(open_context['IsAdmin'] and not open_context.get('UserId'),'Anonymous open mode has platform rights but no private account identity')
            user_context=request('GET','/v1.0/library-context',headers=foreign_auth)
            check(not user_context['IsAdmin'] and user_context['UserId']==foreign['Guid'],'Explicit sign-in retains the real user identity when inference authentication is disabled')
            open_private=request('POST','/v1.0/system-prompts',{'Name':'Open mode private','Scope':'User','Content':'Private account instructions'},foreign_auth)
            anonymous_rows=request('GET','/v1.0/system-prompts',headers={})['Objects']
            check(open_private['Id'] not in {row['Id'] for row in anonymous_rows},'Open anonymous access does not enumerate private records')
            request('GET','/v1.0/system-prompts/'+open_private['Id'],headers={},expected=404)
            request('POST','/v1.0/system-prompts',{'Name':'Not platform','Scope':'System','Content':'x'},foreign_auth,403)
            request('GET','/v1.0/library-context',headers={'Authorization':'Bearer invalid'},expected=401)
            persisted=request('GET','/v1.0/system-prompts?name=Shared%20system',headers=foreign_auth)
            check(len(persisted['Objects'])==1,'Additive library migration and stored items survive restart without duplicate seeds')
            request('GET','/v1.0/system-prompts?pageNumber=2147483647&pageSize=1000',headers=foreign_auth,expected=400)
            request('POST','/v1.0/model-configurations',{'Name':'Mixed','Scope':'User','Configuration':{'SystemPrompt':'Not generation settings'}},foreign_auth,400)
            request('POST','/v1.0/model-configurations',{'Name':'Mixed root','Scope':'User','Content':'Not generation settings','Configuration':{}},foreign_auth,400)
            time.sleep(.2)
            check('Private account instructions' not in (runtime/'server.log').read_text(),'Debug request-body logging withholds personal library content')
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
