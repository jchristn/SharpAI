#!/usr/bin/env python3
"""Validate generated contracts and run saved Postman requests against a disposable native server."""
import argparse
import importlib.util
import json
import os
import pathlib
import platform
import re
import runpy
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--framework',choices=['net8.0','net10.0'],default='net8.0')
    parser.add_argument('--model',type=pathlib.Path,required=True,help='Real local completion GGUF, imported into disposable storage')
    parser.add_argument('--embedding-source', default='https://huggingface.co/leliuga/all-MiniLM-L6-v2-GGUF/resolve/ddf2e25d5b8530422e7b14aa39f33a657ff9aec0/all-MiniLM-L6-v2.Q8_0.gguf',help='Pinned public GGUF source pulled through the saved Postman request')
    parser.add_argument('--output',type=pathlib.Path,default=pathlib.Path('artifacts/rest-reference'))
    args = parser.parse_args()
    model = args.model.resolve(); assert model.is_file()
    output = args.output.resolve(); output.mkdir(parents=True,exist_ok=True)
    subprocess.run(['python3','scripts/generate-rest-reference.py','--check'],cwd=ROOT,check=True)
    subprocess.run(['dotnet','build','src/SharpAI.Server/SharpAI.Server.csproj','-f',args.framework,'-v','quiet'],cwd=ROOT,check=True)
    module_spec = importlib.util.spec_from_file_location('rest_reference',ROOT/'scripts/generate-rest-reference.py')
    module = importlib.util.module_from_spec(module_spec); module_spec.loader.exec_module(module)
    rows = module.inventory(json.loads(module.SPEC.read_text()))
    with tempfile.TemporaryDirectory(prefix='sharpai-rest-reference-') as directory:
        runtime = pathlib.Path(directory)
        port = runpy.run_path(str(ROOT/'scripts/test-tui.py'))['port']()
        settings = json.loads((ROOT/'docker/sharpai.json').read_text())
        settings['Rest'].update({'Hostname':'127.0.0.1','Port':port})
        settings['Telemetry'] = {'Enable':False}
        settings['Rest'].setdefault('Telemetry',{})['Enable'] = True
        settings['Database'] = {'Type':'Sqlite','Filename':'./reference.db'}
        settings['Logging']['ConsoleLogging'] = False
        settings['Storage'] = {'ModelsDirectory':'./models/','TempDirectory':'./temp/'}
        settings['Logging']['LogDirectory'] = './logs/'
        for name in ['models','temp','logs']: (runtime/name).mkdir()
        (runtime/'sharpai.json').write_text(json.dumps(settings))
        log = (runtime/'server.log').open('w')
        env = {**os.environ,'SHARPAI_FORCE_BACKEND':'cpu','SHARPAI_NUM_THREADS':'2','SHARPAI_BATCH_THREADS':'2','SHARPAI_CONTEXT_SIZE':'512'}
        server = subprocess.Popen(['dotnet',str(ROOT/'src/SharpAI.Server/bin/Debug'/args.framework/'SharpAI.Server.dll')],cwd=runtime,env=env,stdout=log,stderr=subprocess.STDOUT)
        base = 'http://127.0.0.1:'+str(port)
        token = None
        def call(method,path,body=None,headers=None,expected=200):
            request_headers = {'Content-Type':'application/json'}
            if token: request_headers['Authorization'] = 'Bearer '+token
            if headers: request_headers.update(headers)
            request = urllib.request.Request(base+path,method=method,data=json.dumps(body).encode() if body is not None else None,headers=request_headers)
            try:
                response = urllib.request.urlopen(request,timeout=30)
            except urllib.error.HTTPError as error:
                assert error.code == expected, method+' '+path+': '+str(error.code)+' '+error.read().decode()
                return None
            assert response.status == expected
            raw = response.read()
            return json.loads(raw) if raw else None
        try:
            for attempt in range(300):
                try:
                    call('GET','/ready'); break
                except (OSError,AssertionError):
                    assert server.poll() is None,'Disposable server exited'
                    time.sleep(.1)
            else:
                log.flush()
                (output/'startup.log').write_text((runtime/'server.log').read_text())
                raise RuntimeError('Server readiness timed out; see startup.log')
            session = call('POST','/v1.0/token',headers={'x-email':'admin@sharpai.local','x-password':'password'})
            token = session['token']
            roundtrip_settings = call('GET','/api/settings')
            assert roundtrip_settings['Rest']['AccessControl']['Mode'] == 'DefaultPermit'
            updated = call('PUT','/api/settings',roundtrip_settings)
            assert updated['Rest']['AccessControl']['Mode'] == 'DefaultPermit'
            assert updated['CreatedUtc'] == roundtrip_settings['CreatedUtc']
            assert updated['SoftwareVersion'] == roundtrip_settings['SoftwareVersion']
            invalid_settings = json.loads(json.dumps(roundtrip_settings))
            invalid_settings['Rest']['AccessControl']['Mode'] = 'not-a-mode'
            call('PUT','/api/settings',invalid_settings,expected=400)
            invalid_settings['Rest']['AccessControl']['Mode'] = 999
            call('PUT','/api/settings',invalid_settings,expected=400)
            invalid_settings['Rest']['AccessControl']['Mode'] = 0
            call('PUT','/api/settings',invalid_settings)
            assert call('GET','/api/settings')['Rest']['AccessControl']['Mode'] == 'DefaultPermit'
            # No assets are installed in the disposable working directory: use the actual build output.
            favicon = urllib.request.urlopen(urllib.request.Request(base+'/favicon.ico',headers={'Authorization':'Bearer '+token}))
            assert favicon.headers.get_content_type() == 'image/x-icon'
            assert favicon.read() == (ROOT/'src/SharpAI.Server/assets/favicon.ico').read_bytes()
            live = call('GET','/openapi.json')
            live_path = output/'openapi.json';live_path.write_text(json.dumps(live,indent=2)+'\n')
            subprocess.run(['python3','scripts/generate-rest-reference.py','--check','--live-openapi',str(live_path)],cwd=ROOT,check=True)
            tenant = call('POST','/v1.0/tenants',{'Name':'Reference fixture'})['Guid']
            user = call('POST','/v1.0/tenants/'+tenant+'/users',{'Email':'fixture@sharpai.local','Password':'fixture-test-password','FirstName':'Reference','LastName':'Fixture'})['Guid']
            credential = call('POST','/v1.0/tenants/'+tenant+'/credentials',{'UserGuid':user,'Name':'Reference fixture'})['guid']
            role = call('POST','/v1.0/tenants/'+tenant+'/roles',{'Name':'Reference fixture'})['Guid']
            permission = call('POST','/v1.0/tenants/'+tenant+'/permissions',{'Name':'Read model fixture','ResourceTypes':['Model'],'OperationTypes':['Read'],'Effect':'Permit'})['Guid']
            assignment = call('POST','/v1.0/tenants/'+tenant+'/assignments',{'UserGuid':user,'RoleGuid':role,'ResourceScope':'Tenant','InheritsToChildren':True})['Guid']
            # Verify the README's tenant boundary and credential owner-ceiling claims through HTTP.
            target = '/v1.0/tenants/'+tenant
            mapping_path = target+'/roles/'+role+'/permissions/'+permission
            call('POST',mapping_path)
            call('GET','/api/tags',headers={'Authorization':'Bearer '+call('POST','/v1.0/token',headers={'x-email':'fixture@sharpai.local','x-password':'fixture-test-password','x-tenant-guid':tenant})['token']})
            fixture_session = call('POST','/v1.0/token',headers={'x-email':'fixture@sharpai.local','x-password':'fixture-test-password','x-tenant-guid':tenant})
            call('GET','/v1.0/tenants/default/users',headers={'Authorization':'Bearer '+fixture_session['token']},expected=403)
            # A newly created credential has no scopes, even when its owner can read models.
            credential_token = call('GET',target+'/credentials/'+credential+'/token')['token']
            credential_headers = {'Authorization':'Bearer '+credential_token}
            call('GET','/api/tags',headers=credential_headers,expected=403)
            grant = call('POST',target+'/credentials/'+credential+'/assignments',{'RoleGuid':role,'ResourceScope':'Tenant','InheritsToChildren':True})['Guid']
            call('GET','/api/tags',headers=credential_headers)
            call('DELETE',target+'/assignments/'+assignment)
            call('GET','/api/tags',headers=credential_headers,expected=403)
            assignment = call('POST',target+'/assignments',{'UserGuid':user,'RoleGuid':role,'ResourceScope':'Tenant','InheritsToChildren':True})['Guid']
            call('GET','/api/tags',headers=credential_headers)
            call('DELETE',target+'/credentials/'+credential+'/assignments/'+grant)
            call('DELETE',mapping_path)
            prompt = call('POST','/v1.0/system-prompts',{'Name':'Reference fixture','Scope':'User','Content':'Be concise.'})['Id']
            configuration = call('POST','/v1.0/model-configurations',{'Name':'Reference fixture','Scope':'User','Configuration':{'MaxTokens':16}})['Id']
            call('POST','/api/import',{'path':str(model),'name':'reference-fixture'})
            # Inspection requires the registry GUID, not its model name or content digest.
            import sqlite3
            with sqlite3.connect(runtime/'reference.db') as db:
                model_guid = db.execute('SELECT guid FROM model_files LIMIT 1').fetchone()[0]
            call('POST','/v1.0/models/presets',{'name':'example-preset','model':'reference-fixture','max_tokens':16})
            user_session = call('POST','/v1.0/token',headers={'x-email':'fixture@sharpai.local','x-password':'fixture-test-password','x-tenant-guid':tenant})['sessionId']
            history = call('GET','/v1.0/api/request-history?pageSize=1')['Objects'][0]['Id']
            values = {'protocol':'http','hostname':'127.0.0.1','port':str(port),'bearerToken':token,'adminBearerToken':token,'tenantGuid':tenant,'userGuid':user,'credentialGuid':credential,'roleGuid':role,'permissionGuid':permission,'assignmentGuid':assignment,'sessionGuid':user_session,'historyId':history,'systemPromptId':prompt,'modelConfigurationId':configuration,'modelGuid':model_guid,'model':'reference-fixture','embeddingModel':args.embedding_source,'modelSource':args.embedding_source,'modelPath':str(model),'importName':'another-reference-import','presetName':'example-preset','newUserEmail':'fixture@sharpai.local','newUserPassword':'fixture-test-password','settingsJson':json.dumps(roundtrip_settings),'maxTokens':'16'}
            environment = {'name':'Disposable reference validation','values':[{'key':key,'value':values[key],'enabled':True,'type':'secret' if 'Token' in key or 'Password' in key else 'default'} for key in values]}
            env_path = runtime/'private.postman_environment.json';env_path.write_text(json.dumps(environment));env_path.chmod(0o600)
            # Read every documented resource family, then exercise mutations without deleting inputs before reads.
            selected = [r for r in rows if r['method'] in ['GET','HEAD']]
            # Session introspection needs the admin's own tenant-independent bearer; no login override in this run.
            selected += [r for r in rows if r['method'] == 'PUT']
            selected += [r for r in rows if r['path'] in ['/api/chat','/api/generate','/v1/chat/completions','/v1/completions','/api/inspect','/api/unload','/api/embed','/v1/embeddings'] and r['method'] == 'POST']
            selected += [r for r in rows if r['method'] == 'POST' and (r['path'].endswith('/duplicate') or '/token/rotate' in r['path'] or r['path'].endswith('/roles/{roleGuid}/permissions/{permissionGuid}') or r['path'].endswith('/credentials/{credentialGuid}/assignments'))]
            selected += [r for r in rows if r['method'] == 'DELETE' and r['path'] not in ['/v1.0/token','/v1.0/api/request-history']]
            # Delete parent resources last; credential assignment id is distinct from the user assignment id.
            selected = [r for r in selected if not (r['method'] == 'DELETE' and '/credentials/{credentialGuid}/assignments/{assignmentGuid}' in r['path'])]
            deletes = [r for r in selected if r['method'] == 'DELETE']
            selected = [r for r in selected if r['method'] != 'DELETE'] + sorted(deletes,key=lambda r: -r['path'].count('/'))
            command = ['postman','collection','run',str(ROOT/'postman/SharpAI.postman_collection.json'),'-e',str(env_path),'--no-report-events']
            command += ['-i',str(uuid.uuid5(uuid.NAMESPACE_URL,'sharpai:POST /api/pull'))]
            for path in ['/api/chat','/api/generate','/v1/chat/completions','/v1/completions']:
                command += ['-i',str(uuid.uuid5(uuid.NAMESPACE_URL,'sharpai:stream:POST '+path))]
            for row in selected:
                command += ['-i',str(uuid.uuid5(uuid.NAMESPACE_URL,'sharpai:'+row['method']+' '+row['path']))]
            # Include existing negative bearer checks without requiring user fixtures or synthetic transport.
            for name in ['Reject anonymous model access','Reject invalid bearer token']:
                command += ['-i',str(uuid.uuid5(uuid.NAMESPACE_URL,'sharpai:regression:Authentication boundary/'+name))]
            with (output/'postman.log').open('w') as report:
                result = subprocess.run(command,cwd=ROOT,stdout=report,stderr=subprocess.STDOUT)
            assert result.returncode == 0,'Postman checks failed; see '+str(output/'postman.log')
            counters = re.search(r'\|\s*assertions\s*\|\s*(\d+)\s*\|\s*(\d+)', (output/'postman.log').read_text())
            assert counters and counters.group(2) == '0', 'Missing or failed Postman assertion summary'
            summary = {'postmanAssertions':int(counters.group(1)),'os':platform.system(),'architecture':platform.machine(),'documentedOperations':len(rows),'selectedSavedRequests':len(selected)+7,'target':'disposable native SQLite server','model':'real local GGUF','framework':args.framework,'postmanExitCode':result.returncode,'tenantAndCredentialBoundaryChecks':6,'cleanup':False,'embeddingSource':args.embedding_source,'remotePull':True,'httpMetricsEnabled':True,'settingsRoundTrip':True,'faviconBundledAsset':True,'limitations':[]}
        finally:
            server.terminate()
            try: server.wait(timeout=15)
            except subprocess.TimeoutExpired: server.kill();server.wait()
            log.close()
    summary['cleanup'] = True
    (output/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    print(json.dumps(summary))


if __name__ == '__main__':
    main()
