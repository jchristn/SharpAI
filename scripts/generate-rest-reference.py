#!/usr/bin/env python3
"""Generate the REST reference and Postman v2.1 collection from checked server contracts.

Use --check in CI to detect stale artifacts; --live-openapi compares a fresh server export.
The runtime OpenAPI snapshot inventories routes; source DTOs supply field documentation.
"""
import argparse
import copy
import functools
import json
import pathlib
import re
import uuid

ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = ROOT / 'docs/api/openapi.json'
BASE = '{{protocol}}://{{hostname}}:{{port}}'
METHODS = ['get', 'head', 'post', 'put', 'delete', 'patch', 'options']
PAGE = [
    {'name': 'pageNumber', 'example': '1', 'description': '1-based page; values below 1 clamp to 1.'},
    {'name': 'pageSize', 'example': '25', 'description': '1 to 1000. Default 100 for general enumeration, 25 for request history.'},
    {'name': 'order', 'example': 'CreatedDescending', 'description': 'CreatedAscending or CreatedDescending; default CreatedDescending.'},
    {'name': 'createdAfter', 'example': '2026-01-01T00:00:00Z', 'description': 'Exclusive creation-time lower bound, UTC.'},
    {'name': 'createdBefore', 'example': '2027-01-01T00:00:00Z', 'description': 'Exclusive creation-time upper bound, UTC.'},
]
HISTORY = [
    PAGE[0], {'name': 'pageSize', 'example': '25', 'description': '1 to 1000; default 25.'},
    {'name': 'method', 'example': 'POST', 'description': 'Exact HTTP method.'},
    {'name': 'statusCode', 'example': '200', 'description': 'Exact HTTP status.'},
    {'name': 'pathContains', 'example': '/api/chat', 'description': 'Substring of request path.'},
    {'name': 'category', 'example': 'generation', 'description': 'generation, embeddings, inference (both), or all.'},
    {'name': 'fromUtc', 'example': '2026-01-01T00:00:00Z', 'description': 'Inclusive UTC lower bound.'},
    {'name': 'toUtc', 'example': '2027-01-01T00:00:00Z', 'description': 'Exclusive UTC upper bound.'},
    {'name': 'tenantId', 'example': '{{tenantGuid}}', 'description': 'Platform administrators may select a tenant; others are bound to their authenticated tenant.'},
    {'name': 'userId', 'example': '{{userGuid}}', 'description': 'Filter by authenticated user ID.'},
]



def contract(method, path):
    """Explicit route-family contracts, reconciled against registrars and handlers."""
    c = {'query': [], 'body': None, 'types': [], 'permission': 'Authenticated bearer; no additional route-level RBAC check.',
         'response': '', 'notes': '', 'folder': 'Management APIs', 'group': 'Infrastructure'}
    if path in ['/health', '/ready']:
        c['permission'] = 'Anonymous infrastructure probe.'
        c['response'] = 'No body.' if method == 'HEAD' else ('Health object: status, version, backend, native_initialized, utc.' if path == '/health' else 'Readiness object: status, version, backend, native_initialized, database_initialized, models_directory, models_directory_ready, logs_directory, logs_directory_ready, telemetry_enabled, telemetry_ready, utc. Returns 503 while dependencies are not ready.')
    elif path in ['/', '/favicon.ico', '/openapi.json', '/swagger', '/metrics']:
        c['response'] = {'/': 'HTML operational homepage.', '/favicon.ico': 'ICO favicon bytes from the bundled assets/favicon.ico under the application directory; independent of the server working directory.', '/openapi.json': 'OpenAPI 3 document describing this server.', '/swagger': 'Swagger UI HTML.', '/metrics': 'Prometheus text exposition of Watson HTTP server instruments; registered only when the HTTP metrics collector is enabled.'}[path]
        if method == 'HEAD': c['response'] = 'No body; 200 when reachable and authenticated.'
    elif path in ['/api/diagnostics/runtime', '/api/diagnostics/memory', '/api/settings']:
        c['group'] = 'Settings and diagnostics'
        c['permission'] = 'Settings Read.' if path == '/api/settings' and method == 'GET' else 'Admin Admin.'
        c['types'] = ['Settings'] if path == '/api/settings' else []
        c['response'] = {'/api/settings': 'Settings object (PascalCase), including configured storage, database, authentication, runtime, telemetry, logging and request-history settings.', '/api/diagnostics/runtime': 'backend_selected, gpu_offload_verified, gpu_offload_note, framework, os, architecture, processors, apis, generation_slots, context_override, llamasharp_version.', '/api/diagnostics/memory': 'working_set_bytes, peak_working_set_bytes, managed_heap_bytes, gc_available_bytes, model_budget_bytes, models inspection array, per_model_observed_available, per_model_observed_bytes, attribution_note.'}[path]
        if method == 'PUT':
            c['body'] = '{{settingsJson}}'
            c['notes'] = 'Full replacement, not PATCH. First GET settings and copy the complete JSON into settingsJson. CreatedUtc and SoftwareVersion are preserved. The file is rewritten; listener/database changes require restart. Do not submit a partial object: omitted values take DTO defaults. Settings may contain secrets; keep exports private. GET responses can be submitted unchanged to PUT; named or defined numeric Rest.AccessControl.Mode values are accepted and invalid modes return 400.'
    elif path in ['/api/inspect', '/api/models/{id}/inspect']:
        c.update(group='Model inspection', permission='Model Read.', types=['ModelInspection'])
        c['response'] = 'ModelInspection object.'
        if method == 'POST':
            c['body'] = {'model': '{{model}}'}
            c['response'] = '{model, sha256, inspection: ModelInspection}.'
        c['notes'] = 'Reads GGUF metadata and estimates without loading weights. GET id must be a model registry GUID, not its name. Missing model: 404; invalid GUID or missing model: 400. Estimates do not establish observed GPU offload or per-model allocations.'
    elif path.startswith('/v1.0/api/request-history'):
        c.update(group='Request and inference history', permission='RequestHistory Delete.' if method == 'DELETE' else 'RequestHistory Read.', types=['RequestHistoryEntry', 'RequestHistorySummary', 'RequestHistoryBucket'])
        if path.endswith('/summary'):
            c['query'] = HISTORY + [{'name': 'bucketMinutes', 'example': '15', 'description': 'Bucket width 1 to 1440 minutes, default 15; includes empty intervals.'}]
            c['response'] = 'RequestHistorySummary with time buckets, counts and average durations.'
        elif path.endswith('/{id}'):
            c['response'] = '{deleted: boolean}.' if method == 'DELETE' else 'RequestHistoryEntry including captured/redacted headers and bodies; 404 if absent.'
        else:
            c['query'] = HISTORY
            c['response'] = '{deletedCount: integer}.' if method == 'DELETE' else 'EnumerationResult<RequestHistoryEntry>; list entries omit bodies.'
        c['notes'] = 'Non-platform principals are bound to their tenant. Inference history and embedding history use category filters on these same endpoints; they are not separate routes. Bulk DELETE removes all matching rows, not just a page; an unfiltered delete clears the caller-visible history scope.'
    elif path == '/v1.0/token':
        c['group'] = 'Authentication'
        c['permission'] = 'Anonymous login with x-email, x-password and optional x-tenant-guid (default tenant when omitted).' if method == 'POST' else 'A valid user session bearer token; API credentials do not identify a session.'
        c['response'] = {'POST': '{token, sessionId, tenantId, userId, expiresUtc}.', 'GET': '{sessionId, tenantId, userId, createdUtc, expiresUtc}.', 'DELETE': '{revoked: true, sessionId}.'}[method]
        c['notes'] = 'No JSON body. Login credentials are HTTP headers. DELETE immediately revokes the current session. Invalid login/session returns 401; missing login headers return 400.'
    elif path == '/v1.0/auth/context':
        c['group'] = 'Authentication'
        c['response'] = '{authEnabled, isAdmin, isTenantAdmin, tenantId, tenantName, userId, capabilities}; capabilities contains read/write flags for Tenant, User, Credential, Role, Permission, Assignment, Session, Audit and Admin.'
        c['notes'] = 'Capability hints do not replace server-side checks. userId reflects PrincipalGuid; for API credentials that is the credential principal identifier.'
    elif path == '/v1.0/api/audit':
        c.update(group='Audit', permission='Audit Read.', query=PAGE + [{'name':'tenantGuid','example':'{{tenantGuid}}','description':'Platform administrators may scope to a tenant; others are constrained to their own tenant.'}], types=['AuditLogEntry'], response='EnumerationResult<AuditLogEntry>.')
    elif path.startswith('/v1.0/tenants'):
        c = management(c, method, path)
    elif path == '/v1.0/library-context' or path.startswith('/v1.0/system-prompts') or path.startswith('/v1.0/model-configurations'):
        c = library(c, method, path)
    elif path.startswith('/v1.0/models/presets'):
        c.update(group='Model presets', permission='Model Read.' if method == 'GET' else ('Model Delete.' if method == 'DELETE' else 'Model Write.'), types=['CreatePresetRequest', 'ModelPreset'])
        c['response'] = 'Preset list.' if path.endswith('/presets') else 'ModelPreset; 404 if not found.'
        if method == 'POST':
            c['body'] = {'name':'{{presetName}}','model':'{{model}}','system':'Answer clearly.','temperature':0.6,'max_tokens':2048,'top_p':0.9,'stop':[]}
            c['response'] = 'Saved ModelPreset.'
        if method == 'DELETE': c['response'] = '{deleted: true, name}.'
        c['notes'] = 'Legacy server-wide presets are separate from scoped prompt/configuration libraries. Duplicate preset names return 409; base model must exist. Use the preset name as model for inference.'
    elif path.startswith('/api/') or path.startswith('/v1/'):
        c = inference(c, method, path)
    else:
        raise ValueError('Missing contract: '+method+' '+path)
    assert c['response'], method+' '+path
    return c


def management(c, method, path):
    segments = path.split('/')
    resource = 'Tenant' if len(segments) <= 4 else {'users':'User','credentials':'Credential','roles':'Role','permissions':'Permission','assignments':'Assignment','sessions':'Session'}[segments[4]]
    c['group'] = {'Tenant':'Tenants','User':'Users','Credential':'Credentials','Role':'Roles','Permission':'Permissions','Assignment':'Assignments','Session':'Sessions'}[resource]
    operation = 'Read' if method == 'GET' else 'Admin'
    c['permission'] = resource+' '+operation+' within the URL tenant; other tenants require platform administrator.'
    c['types'] = [{'Tenant':'Tenant','User':'User','Credential':'Credential','Role':'UserRole','Permission':'Permission','Assignment':'UserRoleAssignment','Session':'AuthSession'}[resource]]
    c['notes'] = 'Server-assigned identity, tenant ownership and protection flags cannot be changed. Built-in/protected records reject disallowed mutations with 403; missing or foreign records return 404 after tenant authorization.'
    item = path.endswith('}')
    c['response'] = c['types'][0]+'.' if item else 'EnumerationResult<'+c['types'][0]+'>.'
    if method == 'GET' and not item: c['query'] = PAGE
    if method == 'DELETE': c['response'] = '{deleted: true} with the applicable tenantId/userId/credentialId/roleId/assignmentId, or {revoked: true} for sessions.'
    if resource == 'Tenant':
        if path == '/v1.0/tenants' or method != 'GET': c['permission'] = 'Platform administrator; create/delete additionally require Admin Admin.'
        if method in ['POST','PUT']: c['body'] = {'Name':'Example tenant'}
        if method == 'PUT': c['body']['Active'] = True
    if resource == 'User':
        c['notes'] += ' Password hashes are redacted. Platform-admin user changes require a platform administrator. Deleting a user also removes owned credentials and role assignments.'
        if method == 'POST':
            c['types'].append('CreateUserRequest')
            c['body'] = {'Email':'{{newUserEmail}}','Password':'{{newUserPassword}}','FirstName':'Example','LastName':'User','IsAdmin':False,'IsTenantAdmin':False}
        if method == 'PUT': c['body'] = {'Email':'{{newUserEmail}}','FirstName':'Example','LastName':'User','Active':True,'IsAdmin':False,'IsTenantAdmin':False}
        if path.endswith('/password'):
            c['body'] = {'Password':'{{newUserPassword}}'}
            c['response'] = '{updated: true}.'
            c['notes'] += ' Resets the password and revokes all user sessions, including later pages. Resetting the protected administrator password requires a platform administrator.'
    if resource == 'Credential':
        c['notes'] += ' Ordinary reads omit usable token material. A credential belongs to a user in this tenant and is constrained by the owner\'s effective permissions.'
        if method == 'POST':
            c['types'].append('CreateCredentialRequest')
            c['body'] = {'UserGuid':'{{userGuid}}','Name':'Example application','ExpiresUtc':None}
            c['response'] = '{guid, userId, tenantId, name, token, tokenType: "Bearer", expiresUtc}; Cache-Control: no-store.'
        if method == 'PUT': c['body'] = {'Name':'Example application','Active':True,'ExpiresUtc':None,'ClearExpiration':False}
        if path.endswith('/token') or path.endswith('/token/rotate'):
            c['query'] = []; c['body'] = None; c['response'] = '{token, tokenType: "Bearer"}; Cache-Control: no-store.'
            c['permission'] = 'Credential Admin for this tenant and target credential.'
            c['notes'] += ' Reveal decrypts a retained token and is audited; legacy hash-only credentials return 409. Rotate immediately invalidates the old token, preserves assignments/status/expiry and is audited. Protected credentials cannot rotate. Rotation does not activate an expired or inactive credential.'
    if resource == 'Role':
        if method in ['POST','PUT']: c['body'] = {'Name':'Example role'}
        if method == 'PUT': c['body']['Active'] = True
        if '/permissions' in path:
            c['group'] = 'Role permissions'; c['body'] = None; c['query'] = []
            c['response'] = 'Permission[] (plain array, not a page).' if method == 'GET' else ('RolePermissionMap.' if method == 'POST' else '{deleted: true}.')
            c['types'] += ['Permission','RolePermissionMap']
            c['notes'] += ' Built-in roles are visible but immutable. Mapped permissions must be active and either global or in this tenant.'
    if resource == 'Permission':
        if method in ['POST','PUT']:
            c['body'] = {'Name':'Read models','ResourceTypes':['Model'],'OperationTypes':['Read'],'Effect':'Permit','Active':True}
        c['notes'] += ' ResourceTypes and OperationTypes must be nonempty. Effect is Permit or Deny; operations must be supported enum values. Explicit deny takes precedence for ordinary principals.'
    if resource == 'Assignment':
        c['types'] += ['CreateAssignmentRequest']
        if method == 'POST': c['body'] = {'UserGuid':'{{userGuid}}','RoleGuid':'{{roleGuid}}','ResourceScope':'Tenant','ResourceGuid':None,'InheritsToChildren':True}
        c['notes'] += ' Supply RoleGuid or a built-in RoleName; role must be active and available in this tenant. Resource scope requires ResourceGuid. ResourceScope is Tenant or Resource; owner ceilings still apply.'
    if '/credentials/{credentialGuid}/assignments' in path:
        c.update(group='Credential assignments', permission='Assignment Read.' if method == 'GET' else 'Assignment Admin.', query=[], types=['CredentialScopeAssignment'])
        c['body'] = {'RoleGuid':'{{roleGuid}}','ResourceScope':'Tenant','ResourceGuid':None,'InheritsToChildren':True} if method == 'POST' else None
        c['response'] = 'CredentialScopeAssignment[] (plain array).' if method == 'GET' else ('CredentialScopeAssignment.' if method == 'POST' else '{deleted: true}.')
        c['notes'] += ' Assignments scope credential access and cannot exceed the owning user\'s rights.'
    elif '/users/{userGuid}/assignments' in path:
        c.update(group='Assignments', permission='Assignment Read.', query=PAGE, types=['UserRoleAssignment'], response='EnumerationResult<UserRoleAssignment>; RoleName is populated when RoleGuid is present.')
    elif path.endswith('/permissions') and resource in ['User','Credential']:
        c.update(group='Effective permissions', permission='Same principal inspecting itself, otherwise Admin Admin; tenant boundary always enforced.', query=[], types=['EffectivePermission'], response='{principalType, principalId, tenantId, count, permissions: [{resourceType, operation, effect, scope, resourceGuid, inheritsToChildren}]}.')
        c['body'] = None
    return c


def library(c, method, path):
    c['group'] = 'Scoped libraries'
    c['permission'] = 'Authenticated principal; library operation permission plus ownership/publication rules. Human users have self-service rights unless explicitly denied. Credential owner ceilings remain enforced.'
    c['types'] = ['LibraryItem', 'GenerationConfiguration']
    c['notes'] = 'System items are shared, Tenant items are tenant defaults, User items are private to their owner (including from administrators). User ownership is derived from the principal. Only platform admins publish System items; tenant admins publish Tenant items in their own tenant. Duplicate to change visibility/ownership. CanEdit/CanDelete are response hints. IDs/timestamps are server assigned.'
    c['response'] = 'LibraryItem.'
    if path == '/v1.0/library-context':
        c['permission'] = "Authenticated bearer; reports the caller's own library context."
        c['response'] = '{IsAdmin, IsTenantAdmin, TenantId, UserId, UserTenantId}.'
    elif method == 'GET' and path.endswith('}'):
        pass
    elif method == 'GET':
        c['query'] = PAGE + [{'name':'name','example':'Example','description':'Exact name; maximum 256 characters.'}]
        c['response'] = 'EnumerationResult<LibraryItem>; visibility filtering occurs before counts and pagination.'
    elif method == 'DELETE': c['response'] = '{deleted: true}.'
    elif path.endswith('/duplicate'): c['body'] = {'Name':'My copy','Scope':'User'}
    else:
        c['body'] = {'Name':'My saved item','Description':'Reusable defaults','Scope':'User'}
        if 'system-prompts' in path:
            c['body']['Content'] = 'Answer clearly and accurately.'
            c['notes'] += ' Content is required, at most 1048576 characters. Configuration must be absent.'
        else:
            c['body']['Configuration'] = {'Temperature':0.6,'MaxTokens':2048,'TopP':0.9,'Stop':[],'StreamResponse':True,'JsonOutput':False,'DisplayThinking':False}
            c['notes'] += ' Configuration is required; Content must be absent. Stop permits at most 64 non-null entries of at most 2048 characters each.'
    c['query'] += [{'name':'tenantId','example':'{{tenantGuid}}','description':'Selected tenant; only platform admins may select another tenant. Does not expose another user\'s private items.'}]
    return c


def inference(c, method, path):
    openai = path.startswith('/v1/')
    c.update(folder='OpenAI APIs' if openai else 'Ollama APIs', group='Inference', permission='Inference Execute.')
    family = 'OpenAI' if openai else 'Ollama'
    if path in ['/api/chat','/v1/chat/completions','/api/generate','/v1/completions','/api/embed','/v1/embeddings']:
        kind = 'Embeddings' if 'embed' in path else ('ChatCompletion' if 'chat' in path else 'Completion')
        c['types'] = [family+'Generate'+kind+'Request', family+'Generate'+kind+'Result']
        c['body'] = {'model':'{{embeddingModel}}' if kind == 'Embeddings' else '{{model}}'}
        if kind == 'Embeddings':
            c['body']['input'] = ['Hello world','A second input']
            c['response'] = 'OpenAIGenerateEmbeddingsResult: object=list, data entries (object=embedding, embedding float array, index), model, usage.' if openai else 'OllamaGenerateEmbeddingsResult: model, embeddings (one float array per input), and available usage/timing fields.'
            c['notes'] = 'model and input are required; input accepts a string or string array, not token ID arrays. Select a model with embedding support; incompatible models return 403. No streaming. Output dimensions follow the model.'
        else:
            c['body'].update({'messages':[{'role':'user','content':'Hello'}]} if kind == 'ChatCompletion' else {'prompt':'Explain local inference in one sentence.'})
            c['body']['stream'] = False
            c['body'].update({'max_tokens':2048} if openai else {'options':{'num_predict':2048}})
            c['response'] = (family+'Generate'+kind+'Result; see the response DTO and streaming contract below.')
            c['notes'] = 'model and '+('messages' if kind == 'ChatCompletion' else 'prompt')+' are required. Send the full conversation on each chat call; server sessions authenticate users and do not store chat context. Output tokens include hidden reasoning. API default budget is 128 when not supplied (or preset default for chat); UI Balanced configuration explicitly supplies 2048. Context preflight may reject prompt plus output budget with 400. Capacity/admission failures return 429. Unsupported completion model returns 403; missing model returns 404.'
            c['notes'] += ' OpenAI defaults to nonstreaming; stream=true uses SSE.' if openai else ' Ollama defaults to streaming when stream is omitted; stream=false returns a single JSON object.'
        return c
    c.update(group='Models', permission='Model Read.')
    if path == '/v1/models':
        c['query'] = [{'name':'include_context','example':'true','description':'Add trained_context_length and effective_context_length without loading weights.'}]
        c['response'] = '{object: "list", data: [{id, object: "model", created: Unix seconds, owned_by: "sharpai"}]}; returns all registered models.'
    elif path == '/api/tags':
        c['types'] = ['OllamaLocalModel']
        c['response'] = '{models: [{name, model, modified_at, size, digest, details: {parent_model, format, family, families, parameter_size, quantization_level}, capabilities: {embeddings: boolean, completions: boolean}}]}; returns all registered models, empty array when none. Pagination/filter query parameters are not applied by this route.'
    elif path == '/api/ps':
        c['types'] = ['OllamaListRunningModelsResult','OllamaRunningModel']
        c['response'] = 'OllamaListRunningModelsResult containing currently loaded model entries.'
        c['notes'] = 'size_vram reports full model size on a selected CUDA/Metal backend and zero on CPU; it is not measured GPU allocation. expires_at reflects configured keep-alive eviction or is null when disabled.'
    elif path == '/api/version':
        c['permission'] = 'Authenticated bearer; no additional route-level RBAC check.'
        c['response'] = '{version: string}.'
    elif path == '/api/pull':
        c.update(permission='Model Write.', body={'model':'{{modelSource}}','stream':True}, types=['OllamaPullModelRequest'], response='NDJSON progress records, ending with {status: "success", downloaded, percent: 1}; errors after headers appear as {error: string}.')
        c['notes'] = 'model (or legacy name) is a Hugging Face organization/repository or validated HTTPS repository/tree/blob/resolve GGUF URL. The current handler always streams progress, including when stream=false is sent. insecure/username/password fields are not used for registry authentication; configure the server Hugging Face token. Revisions resolve to pinned commits; exact GGUF file selection is honored. Read every record and require explicit success. Never automatically retry an ambiguous pull.'
    elif path == '/api/import':
        c.update(permission='Model Write.', body={'path':'{{modelPath}}','name':'{{importName}}'}, types=['ImportModelRequest','OllamaLocalModel'], response='Imported model metadata: name, model, modified_at, size, digest, details (parent_model, format, family, families, parameter_size, quantization_level), and capabilities (embeddings/completions booleans).')
        c['notes'] = 'path is a GGUF file readable on the server filesystem, not a file upload or client-side path. name is optional, defaults to filename stem. Copies into managed storage, detects capabilities and registers the model; no Hugging Face token needed. 400 invalid path/file; 404 missing file; 409 duplicate name; 500 import failure.'
    elif path in ['/api/delete','/api/unload','/api/show']:
        c['body'] = {'model' if path == '/api/show' else 'name':'{{model}}'}
        c['types'] = [{' /api/delete':'OllamaDeleteModelRequest',' /api/unload':'OllamaUnloadModelRequest',' /api/show':'OllamaShowModelInfoRequest'}[' '+path]]
        c['permission'] = 'Model Read.' if path == '/api/show' else 'Model Write.'
        c['response'] = {'/api/delete':'200 with no JSON result on deletion; 404 with an error if not registered.', '/api/unload':'{status, model, message}; unloads resident weights but keeps files and registry. Without a model, unloads all resident models.', '/api/show':'OllamaShowModelInfoResult with model metadata, parameters and capabilities.'}[path]
        if path == '/api/show': c['types'].append('OllamaShowModelInfoResult')
        c['notes'] = 'show requires model; delete and unload use name. Deletion removes the registered model and managed files. Unload preserves downloaded files; an empty object unloads all resident models.'
    else: raise ValueError('Missing inference contract '+path)
    return c


def anchor(method, path):
    return method.lower()+'-'+re.sub('[^a-z0-9]+','-',path.lower()).strip('-')


def json_text(value):
    return value if isinstance(value,str) else json.dumps(value,indent=2,ensure_ascii=False)


def required_inputs(row):
    method = row['method']; path = row['path']
    if path == '/v1.0/token' and method == 'POST': return 'x-email and x-password headers. Optional x-tenant-guid selects the login tenant; no JSON body.'
    if row['contract']['body'] is None: return 'No JSON body. Supply all named path parameters and authentication when required.'
    if path == '/api/settings': return 'A complete settings object; preserve fields fetched by GET. Named enum values returned by GET can be sent unchanged.'
    if path in ['/api/chat','/v1/chat/completions']: return 'model and messages (conversation array). Each message supplies role/content or the applicable tool-call/result fields.'
    if path in ['/api/generate','/v1/completions']: return 'model and prompt; OpenAI prompt accepts a text string or nonempty string array.'
    if path in ['/api/embed','/v1/embeddings']: return 'model and input (text string or nonempty text array).'
    if path == '/api/pull': return 'model, or legacy name, identifying a supported Hugging Face source.'
    if path == '/api/import': return 'path; name is optional.'
    if path in ['/api/show','/api/inspect']: return 'model.'
    if path == '/api/delete': return 'name.'
    if path == '/api/unload': return 'No required model field; name selects one model, an empty object unloads all.'
    if path.startswith('/v1.0/models/presets'): return 'name and model; other preset fields are optional.'
    if 'system-prompts' in path or 'model-configurations' in path:
        if path.endswith('/duplicate'): return 'Name; omitted Content or Configuration inherits the source payload. Scope defaults to User.'
        return 'Name and '+('Content' if 'system-prompts' in path else 'Configuration')+'; Scope defaults to User. Description is optional.'
    if path.endswith('/password'): return 'Password; must be nonblank.'
    if path.endswith('/users'): return 'Email and Password; other profile/admin fields are optional.'
    if '/users/{userGuid}' in path and method == 'PUT': return 'Email; optional FirstName, LastName, IsAdmin, IsTenantAdmin, Active.'
    if '/credentials/{credentialGuid}/assignments' in path: return 'RoleGuid or built-in RoleName; ResourceGuid is required when ResourceScope is Resource.'
    if path.endswith('/assignments'): return 'UserGuid and RoleGuid or built-in RoleName; ResourceGuid is required when ResourceScope is Resource.'
    if path.endswith('/credentials'): return 'UserGuid; Name and ExpiresUtc are optional.'
    if '/permissions' in path: return 'Nonempty ResourceTypes and OperationTypes arrays; Effect is Permit (default) or Deny. Name and Active are optional; PUT replaces the permission fields.'
    if '/tenants' in path or '/roles' in path or '/credentials' in path: return 'Name; Active is optional for updates. Credential ExpiresUtc changes expiry; ClearExpiration=true removes it.'
    raise ValueError('Required-input review missing: '+method+' '+path)


def operation_description(row):
    c = row['contract']
    lines = [row['summary'], '', '**Authorization:** '+c['permission'], '', '**Required inputs:** '+required_inputs(row)]
    if c['query']:
        lines += ['', '**Query parameters (optional):**'] + ['- `'+p['name']+'`: '+p['description'] for p in c['query']]
    lines += ['', '**Success response:** '+c['response']]
    if c['types']: lines += ['', '**Field reference:** '+', '.join('[`'+name+'`](#'+name.lower()+')' for name in c['types'])+' in REST_API.md.']
    if c['notes']: lines += ['', c['notes']]
    if row['description']: lines += ['', '**Server description:** '+row['description'].replace('\u2014',';')]
    lines += ['', '**Declared responses:** '+', '.join(row['responses'])+'. Protected operations can also return 401; authorization checks can return 403.']
    return '\n'.join(lines)


def source_properties(path):
    """Extract public DTO properties and their nearby XML docs; never execute source."""
    source = path.read_text(encoding='utf-8-sig')
    props = []
    for match in re.finditer(r'public\s+(?:override\s+)?([\w<>,?\[\]. ]+)\s+(\w+)\s*\{', source):
        type_name = match.group(1).strip()
        if type_name.startswith(('class ', 'enum ', 'static ', 'sealed ')): continue
        # Properties contain get/set; nested classes and methods are excluded.
        following = source[match.end():match.end()+180]
        if not re.match(r'\s*(?:get|set)\b',following): continue
        before = source[:match.start()]
        nearby = re.search(r'((?:[ \t]*///[^\n]*\n)+)\s*((?:\[[^\n]*\]\s*)*)$', before)
        metadata = nearby.group(0) if nearby else ''
        if '[JsonIgnore]' in metadata: continue
        names = re.findall(r'JsonPropertyName\("([^"]+)"\)',metadata)
        name = names[-1] if names else match.group(2)
        comments = re.findall(r'///\s?(.*)',metadata)
        description = ' '.join(comments)
        summaries = re.findall(r'<summary>(.*?)</summary>', description)
        if summaries: description = summaries[-1]
        description = re.sub(r'<see\s+cref="([^"]+)"\s*/>',r'\1',description)
        description = re.sub(r'<[^>]+>','',description).strip().replace('|','\\|').replace('\u2014',';')
        if path.name == 'OllamaDeleteModelRequest.cs' and name == 'name': description = 'Registered model name to delete; required.'
        if path.name == 'Settings.cs' and name == 'Auth': description = 'Authentication configuration; bearer authentication is mandatory in the server. Legacy false is ignored.'
        props.append({'name':name,'type':type_name,'description':description or 'See source validation and operation-specific requirements.'})
    return props


@functools.cache
def schemas():
    files = []
    for folder in ['src/SharpAI/Models/Ollama','src/SharpAI/Models/OpenAI','src/SharpAI.Server/Classes/Requests','src/SharpAI.Server/Classes/Settings']:
        files += list((ROOT/folder).glob('*.cs'))
    for name in ['EnumerationResult','RequestHistoryEntry','RequestHistorySummary','RequestHistoryBucket','LibraryItem','GenerationConfiguration','ModelPreset','ModelFile']:
        files.append(ROOT/'src/SharpAI/Models'/ (name+'.cs'))
    for name in ['Tenant','User','Credential','UserRole','Permission','UserRoleAssignment','CredentialScopeAssignment','RolePermissionMap','AuthSession','AuditLogEntry','EffectivePermission']:
        files.append(ROOT/'src/SharpAI/Security'/(name+'.cs'))
    files += [ROOT/'src/SharpAI/Database/DatabaseSettings.cs', ROOT/'src/SharpAI/Operations/ModelInspection.cs',ROOT/'src/SharpAI.Server/API/REST/Routes/AdministrationRoutes.cs']
    result = []
    for path in sorted(files):
        properties = source_properties(path)
        if properties:
            result.append({'name':'UpdateRequest' if path.stem == 'AdministrationRoutes' else path.stem,'path':str(path.relative_to(ROOT)),'properties':properties})
    return result


def inventory(spec):
    rows = []
    for path in sorted(spec['paths']):
        for method in METHODS:
            if method not in spec['paths'][path]: continue
            op = spec['paths'][path][method]
            rows.append({'method':method.upper(),'path':path,'summary':op.get('summary',method.upper()+' '+path),
                         'description':op.get('description',''),'responses':op.get('responses',{}),
                         'contract':contract(method.upper(),path)})
    return rows


def check_inventory(rows):
    keys = {row['method']+' '+row['path'] for row in rows}
    sources = [ROOT/'src/SharpAI.Server/Program.cs'] + list((ROOT/'src/SharpAI.Server/API/REST/Routes').glob('*.cs'))
    for source in sources:
        text = source.read_text(encoding='utf-8-sig')
        pattern = r'(?:_Server|ctx\.Server)\.(Get|Head|Post|Put|Delete)(?:<[^\n]+?>)?\("([^"\n]+)"'
        for match in re.finditer(pattern,text):
            key = match.group(1).upper()+' '+match.group(2)
            assert key in keys, 'Server route missing from OpenAPI snapshot/reference: '+key
    for resource in ['system-prompts','model-configurations']:
        for suffix in ['', '/{id}', '/{id}/duplicate']:
            methods = ['GET','POST'] if not suffix else (['POST'] if suffix.endswith('duplicate') else ['GET','PUT','DELETE'])
            for method in methods: assert method+' /v1.0/'+resource+suffix in keys
    for row in json.loads((ROOT/'dashboard/src/api/operations.json').read_text()):
        assert row['method']+' '+row['path'] in keys, row


def response_example(row):
    """Illustrative wire shapes only; examples contain no captured secrets or personal data."""
    path = row['path']; method = row['method']; c = row['contract']
    if method == 'HEAD' or path == '/api/delete': return ''
    if path == '/': return '<html><body>SharpAI</body></html>'
    if path == '/favicon.ico': return ''
    if path == '/swagger': return '<html><body>Swagger UI</body></html>'
    if path == '/openapi.json': return {'openapi':'3.0.3','info':{'title':'SharpAI Server API','version':'4.0.1'},'paths':{}}
    if path == '/metrics': return '# Prometheus exposition; enabled instruments depend on configuration\n'
    if path == '/health': return {'status':'healthy','version':'4.0.1','backend':'cpu','native_initialized':True,'utc':'2026-01-01T00:00:00Z'}
    if path == '/ready': return {'status':'ready','version':'4.0.1','native_initialized':True,'database_initialized':True,'models_directory_ready':True,'logs_directory_ready':True,'telemetry_ready':True}
    if path == '/v1.0/token':
        if method == 'DELETE': return {'revoked':True,'sessionId':'example-session-id'}
        result = {'sessionId':'example-session-id','tenantId':'default','userId':'example-user-id','expiresUtc':'2026-01-02T00:00:00Z'}
        if method == 'POST': result['token'] = 'EXAMPLE_SESSION_TOKEN_NOT_A_REAL_SECRET'
        else: result['createdUtc'] = '2026-01-01T00:00:00Z'
        return result
    if method == 'DELETE':
        if '/sessions/' in path: return {'revoked':True}
        if path == '/v1.0/api/request-history': return {'deletedCount':1}
        return {'deleted':True}
    if path.endswith('/password'): return {'updated':True}
    if '/credentials/' in path and (path.endswith('/token') or path.endswith('/token/rotate')):
        return {'token':'EXAMPLE_CREDENTIAL_TOKEN_NOT_A_REAL_SECRET','tokenType':'Bearer'}
    if path.endswith('/credentials') and method == 'POST':
        return {'guid':'example-credential-id','userId':'example-user-id','tenantId':'default','name':'Example application','token':'EXAMPLE_CREDENTIAL_TOKEN_NOT_A_REAL_SECRET','tokenType':'Bearer'}
    if path == '/api/pull': return '{"status":"pulling manifest"}\n{"status":"success","downloaded":19077344,"percent":1}\n'
    if path == '/api/version': return {'version':'4.0.1'}
    if path in ['/api/tags','/api/ps']: return {'models':[]}
    if path == '/v1/models': return {'object':'list','data':[]}
    if path in ['/api/chat','/api/generate']:
        result = {'model':'example-model','created_at':'2026-01-01T00:00:00Z','done':True,'done_reason':'stop','prompt_eval_count':4,'eval_count':2}
        if path == '/api/chat': result['message'] = {'role':'assistant','content':'Hello.'}
        else: result['response'] = 'Hello.'
        return result
    if path in ['/v1/chat/completions','/v1/completions']:
        choice = {'index':0,'finish_reason':'stop'}
        if path == '/v1/chat/completions': choice['message'] = {'role':'assistant','content':'Hello.'}
        else: choice['text'] = 'Hello.'
        return {'id':'example-request-id','object':'chat.completion' if 'chat' in path else 'text_completion','created':1767225600,'model':'example-model','choices':[choice],'usage':{'prompt_tokens':4,'completion_tokens':2,'total_tokens':6}}
    if path == '/api/embed': return {'model':'example-embedding-model','embeddings':[[0.1,0.2],[0.3,0.4]]}
    if path == '/v1/embeddings': return {'object':'list','model':'example-embedding-model','data':[{'object':'embedding','index':0,'embedding':[0.1,0.2]},{'object':'embedding','index':1,'embedding':[0.3,0.4]}],'usage':{'prompt_tokens':6,'total_tokens':6}}
    if path == '/api/unload': return {'status':'success','model':'example-model','message':"Model 'example-model' unloaded from memory"}
    if path == '/v1.0/auth/context': return {'authEnabled':True,'isAdmin':False,'isTenantAdmin':False,'tenantId':'default','userId':'example-user-id','capabilities':{'User':{'read':False,'write':False}}}
    if path == '/v1.0/library-context': return {'IsAdmin':False,'IsTenantAdmin':False,'TenantId':'default','UserTenantId':'default','UserId':'example-user-id'}
    if 'plain array' in c['response']: return []
    if c['response'].startswith('EnumerationResult<'):
        return {'Success':True,'MaxResults':25,'Skip':0,'TotalRecords':0,'RecordsRemaining':0,'EndOfResults':True,'Objects':[]}
    if 'permissions: [' in c['response']: return {'principalType':'User' if '/users/' in path else 'Credential','principalId':'example-principal-id','tenantId':'default','count':0,'permissions':[]}
    if path.endswith('/presets') and method == 'GET': return []
    if path.startswith('/v1.0/models/presets'):
        return {'GUID':'00000000-0000-0000-0000-000000000001','Name':'example-preset','ModelName':'example-model','SystemPrompt':'Answer clearly.','Temperature':0.6,'MaxTokens':2048,'TopP':0.9,'Stop':[],'CreatedUtc':'2026-01-01T00:00:00Z','LastUpdateUtc':'2026-01-01T00:00:00Z'}
    if path == '/api/import':
        return {'name':'example-model','model':'example-model','modified_at':'2026-01-01T00:00:00Z','size':19077344,'digest':'example-sha256','details':{'parent_model':'','format':'gguf','family':'llama','families':['llama'],'parameter_size':'15000000','quantization_level':'Q4_0'},'capabilities':{'embeddings':False,'completions':True}}
    if c['body'] is not None and isinstance(c['body'],dict) and '/v1.0/' in path:
        result = copy.deepcopy(c['body'])
        result.pop('Password', None)
        result.pop('ClearExpiration', None)
        result['Id' if 'libraries' in path or 'system-prompts' in path or 'model-configurations' in path else 'Guid'] = 'example-record-id'
        return result
    if path == '/api/diagnostics/runtime':
        return {'backend_selected':'cpu','gpu_offload_verified':False,'gpu_offload_note':'Consult native model-load logs.','framework':'.NET 8','os':'example OS','architecture':'X64','processors':4,'apis':['ollama','openai'],'generation_slots':1,'context_override':0,'llamasharp_version':'example-version'}
    if path == '/api/diagnostics/memory':
        return {'working_set_bytes':0,'peak_working_set_bytes':0,'managed_heap_bytes':0,'gc_available_bytes':0,'model_budget_bytes':0,'models':[],'per_model_observed_available':False,'attribution_note':'Shared native allocations cannot be reliably attributed to individual models.'}
    if c['types']:
        preferred = c['types'][-1] if path == '/api/show' else c['types'][0]
        table = next((value for value in schemas() if value['name'] == preferred), None)
        if table:
            result = {}
            for prop in table['properties']:
                name = prop['name']; value_type = prop['type']
                if name in ['PasswordSha256','SecretSha256','AccessKey','BearerTokenEncrypted','TokenSha256','Token']: continue
                if value_type.endswith('?'): continue
                if value_type.startswith('List<') or value_type.endswith('[]'): result[name] = []
                elif value_type in ['int','long','float','double','decimal']: result[name] = 0
                elif value_type == 'bool': result[name] = False
                elif value_type == 'DateTime': result[name] = '2026-01-01T00:00:00Z'
                elif value_type == 'string': result[name] = 'example'
                else: continue
            if path == '/api/inspect': return {'model':'example-model','sha256':'example-sha256','inspection':result}
            return result
    raise ValueError('Missing saved response example: '+method+' '+path)


def request_item(row):
    c = row['contract']; method = row['method']; path = row['path']
    raw_path = re.sub(r'\{(\w+)\}',r'{{\1}}',path)
    # Generic id has different domains on inspection, history and libraries.
    id_name = 'modelGuid' if '/api/models/' in path else ('historyId' if 'request-history' in path else ('systemPromptId' if 'system-prompts' in path else 'modelConfigurationId'))
    raw_path = raw_path.replace('{{id}}','{{'+id_name+'}}').replace('{{name}}','{{presetName}}')
    url = {'raw':BASE+raw_path,'protocol':'{{protocol}}','host':['{{hostname}}'],'port':'{{port}}','path':raw_path.lstrip('/').split('/') if raw_path != '/' else []}
    if c['query']:
        url['query'] = [{'key':q['name'],'value':q['example'],'description':q['description'],'disabled':True} for q in c['query']]
    request = {'method':method,'header':[],'url':url,'description':operation_description(row)}
    if c['body'] is not None:
        request['header'].append({'key':'Content-Type','value':'application/json','type':'text'})
        raw_body = json_text(c['body'])
        if path in ['/api/chat','/api/generate','/v1/chat/completions','/v1/completions']:
            raw_body = raw_body.replace(': 2048', ': {{maxTokens}}')
        request['body'] = {'mode':'raw','raw':raw_body,'options':{'raw':{'language':'json'}}}
    if path in ['/health','/ready'] or (path == '/v1.0/token' and method == 'POST'): request['auth'] = {'type':'noauth'}
    if path == '/v1.0/token' and method == 'POST':
        request['header'] += [{'key':'x-email','value':'{{loginEmail}}'}, {'key':'x-password','value':'{{loginPassword}}'}, {'key':'x-tenant-guid','value':'{{tenantGuid}}'}]
    item = {'id':str(uuid.uuid5(uuid.NAMESPACE_URL,'sharpai:'+method+' '+path)),'name':method+' '+path,'request':request,'response':[]}
    example = response_example(row)
    if example is not None:
        mime = 'application/json'
        if path == '/api/pull': mime = 'application/x-ndjson'
        elif path in ['/','/swagger']: mime = 'text/html'
        elif path == '/metrics': mime = 'text/plain'
        elif path == '/favicon.ico': mime = 'image/x-icon'
        headers = [{'key':'Content-Type','value':mime}]
        if '/credentials' in path and (path.endswith('/token') or path.endswith('/token/rotate') or method == 'POST' and path.endswith('/credentials')):
            headers.append({'key':'Cache-Control','value':'no-store'})
        item['response'].append({'name':'200 illustrative shape (not a captured response)','originalRequest':copy.deepcopy(request),'status':'OK','code':200,'header':headers,'body':json_text(example)})
    if request.get('auth',{}).get('type') != 'noauth':
        item['response'].append({'name':'401 bearer challenge','originalRequest':copy.deepcopy(request),'status':'Unauthorized','code':401,'header':[{'key':'Content-Type','value':'application/json'},{'key':'WWW-Authenticate','value':'Bearer'}],'body':'{"error":"Bearer authentication is required."}'})
    scripts = ["pm.test('Expected HTTP status', () => pm.response.to.have.status(200));"]
    if path == '/v1.0/token' and method == 'POST':
        scripts += ["if (pm.response.code === 200) {", "  const session = pm.response.json();", "  pm.test('Session token returned', () => pm.expect(session.token).to.be.a('string').and.not.empty);", "  pm.environment.set('bearerToken', session.token);", "  pm.environment.set('tenantGuid', session.tenantId);", "  pm.environment.set('userGuid', session.userId);", "}"]
    if path.endswith('/token') and '/credentials/' in path or path.endswith('/token/rotate') or (method == 'POST' and path.endswith('/credentials')):
        scripts += ["pm.test('Secret response is not cached', () => pm.expect(pm.response.headers.get('Cache-Control')).to.include('no-store'));"]
    if path in ['/api/embed','/v1/embeddings']:
        scripts += [
            "const embeddingResult = pm.response.json();",
            "const vectors = embeddingResult.embeddings || (embeddingResult.data || []).map(item => item.embedding);",
            "pm.test('One vector per input', () => pm.expect(vectors.length).to.eql(2));",
            "pm.test('Finite nonzero vectors with consistent dimensions', () => { pm.expect(vectors[0].length).to.be.above(0); for (const vector of vectors) { pm.expect(vector.length).to.eql(vectors[0].length); pm.expect(vector.every(value => typeof value === 'number' && Number.isFinite(value))).to.eql(true); pm.expect(vector.some(value => value !== 0)).to.eql(true); } });"
        ]
    if path == '/metrics':
        scripts += [
            "pm.test('Prometheus content type', () => pm.expect(pm.response.headers.get('Content-Type')).to.include('text/plain'));",
            "pm.test('HTTP request measurements recorded', () => { const samples = pm.response.text().split(/\\r?\\n/).filter(line => line.startsWith('http_server_request_duration') && line.includes('_count')); pm.expect(samples.some(line => Number(line.trim().split(/\\s+/).pop()) > 0)).to.eql(true); });"
        ]
    if path == '/favicon.ico' and method == 'GET':
        scripts += ["pm.test('ICO content type', () => pm.expect(pm.response.headers.get('Content-Type')).to.include('image/x-icon'));"]
    if path == '/api/pull':
        scripts += ["const records = pm.response.text().trim().split(/\\r?\\n/).filter(Boolean).map(line => JSON.parse(line));", "pm.test('Verified pull completed', () => { pm.expect(records.some(record => record.error)).to.eql(false); pm.expect(records[records.length - 1].status).to.eql('success'); });"]
    if path in ['/api/chat','/api/generate']:
        scripts += ["pm.test('Generation completed', () => pm.expect(pm.response.json().done).to.eql(true));"]
    if path in ['/v1/chat/completions','/v1/completions']:
        scripts += ["pm.test('Completion choices returned', () => pm.expect(pm.response.json().choices).to.be.an('array').and.not.empty);"]
    if method != 'HEAD' and path not in ['/','/favicon.ico','/swagger','/metrics','/api/pull','/api/delete']:
        scripts += ["pm.test('JSON response', () => pm.response.to.be.json);"]
    item['event'] = [{'listen':'test','script':{'type':'text/javascript','exec':scripts}}]
    return item


def streaming_item(row):
    item = request_item(row)
    item['id'] = str(uuid.uuid5(uuid.NAMESPACE_URL,'sharpai:stream:'+row['method']+' '+row['path']))
    item['name'] += ' (streaming)'
    body = copy.deepcopy(row['contract']['body']); body['stream'] = True
    openai = row['path'].startswith('/v1/')
    if openai: body['stream_options'] = {'include_usage':True}
    item['request']['body']['raw'] = json_text(body).replace(': 2048', ': {{maxTokens}}')
    item['request']['description'] += '\n\nThis variant verifies terminal records and, for OpenAI, an explicit usage chunk.'
    if openai:
        scripts = [
            "pm.test('SSE request succeeds', () => pm.response.to.have.status(200));",
            "const data = pm.response.text().split(/\\r?\\n/).filter(line => line.startsWith('data:')).map(line => line.slice(5).trim());",
            "pm.test('SSE terminates explicitly', () => pm.expect(data[data.length - 1]).to.eql('[DONE]'));",
            "const chunks = data.filter(line => line !== '[DONE]').map(line => JSON.parse(line));",
            "pm.test('Finish reason received', () => pm.expect(chunks.some(chunk => (chunk.choices || []).some(choice => choice.finish_reason))).to.eql(true));",
            "pm.test('Requested usage received', () => pm.expect(chunks.some(chunk => chunk.usage && chunk.choices.length === 0)).to.eql(true));"
        ]
        mime = 'text/event-stream'
        example = 'data: {"choices":[{"index":0,"finish_reason":"stop"}]}\n\ndata: [DONE]\n\n'
    else:
        scripts = [
            "pm.test('NDJSON request succeeds', () => pm.response.to.have.status(200));",
            "const records = pm.response.text().trim().split(/\\r?\\n/).filter(Boolean).map(line => JSON.parse(line));",
            "pm.test('No stream error', () => pm.expect(records.some(record => record.error)).to.eql(false));",
            "pm.test('Terminal done record received', () => pm.expect(records[records.length - 1].done).to.eql(true));"
        ]
        mime = 'application/x-ndjson'
        example = '{"model":"example-model","done":true,"done_reason":"stop"}\n'
    item['event'] = [event for event in item['event'] if event['listen'] != 'test'] + [{'listen':'test','script':{'type':'text/javascript','exec':scripts}}]
    item['response'] = [{'name':'200 illustrative stream (not captured)','originalRequest':copy.deepcopy(item['request']),'status':'OK','code':200,'header':[{'key':'Content-Type','value':mime}],'body':example}]
    return item


def collection(rows, original):
    c = {'info':copy.deepcopy(original['info']),'auth':{'type':'bearer','bearer':[{'key':'token','value':'{{bearerToken}}','type':'string'}]},'variable':[], 'item':[]}
    c['info']['description'] = 'Complete SharpAI REST collection. See REST_API.md. Three top-level folders cover Ollama, OpenAI and Management APIs, including authentication, scoped libraries, history, diagnostics and infrastructure. Set private environment values for bearerToken and loginPassword; no real secrets are committed. Login captures its returned session into the active environment. Select requests in the order appropriate to your records; this catalog includes mutating operations and secret reveal/rotation. Regression scenarios preserve negative authentication and TUI contract assertions. This collection is generated by scripts/generate-rest-reference.py; HTTP routes come from docs/api/openapi.json and request/response details from checked source contracts.'
    values = {'protocol':'http','hostname':'localhost','port':'8000','bearerToken':'','adminBearerToken':'','tenantGuid':'default','loginEmail':'admin@sharpai.local','loginPassword':'','userGuid':'','credentialGuid':'','roleGuid':'','permissionGuid':'','assignmentGuid':'','sessionGuid':'','modelGuid':'','historyId':'','systemPromptId':'','modelConfigurationId':'','model':'','maxTokens':'2048','embeddingModel':'','modelSource':'https://huggingface.co/ggml-org/models-moved/resolve/499bc8821c6b12b4e53c5bffcb21ec206f212d81/tinyllamas/stories15M-q4_0.gguf','modelPath':'/app/models/example.gguf','importName':'example-import','presetName':'example-preset','newUserEmail':'example@sharpai.local','newUserPassword':'','settingsJson':''}
    c['variable'] = [{'key':key,'value':values[key],'type':'string'} for key in values]
    for folder_name in ['Ollama APIs','OpenAI APIs','Management APIs']:
        folder = {'name':folder_name,'item':[]}
        groups = {}
        for row in rows:
            if row['contract']['folder'] != folder_name: continue
            group = row['contract']['group']
            if group not in groups:
                groups[group] = {'name':group,'item':[]}; folder['item'].append(groups[group])
            groups[group]['item'].append(request_item(row))
            if row['path'] in ['/api/chat','/api/generate','/v1/chat/completions','/v1/completions']:
                groups[group]['item'].append(streaming_item(row))
        if folder_name == 'Management APIs':
            scenarios = json.loads((ROOT/'postman/regression-scenarios.json').read_text())
            # Preserve maintained assertions and assign stable IDs for CLI selection.
            for scenario in scenarios:
                for item in scenario['item']:
                    item['id'] = str(uuid.uuid5(uuid.NAMESPACE_URL,'sharpai:regression:'+scenario['name']+'/'+item['name']))
            folder['item'].append({'name':'Regression scenarios','item':scenarios})
        c['item'].append(folder)
    return c


def reference(rows, types):
    intro = (ROOT/'docs/api/reference-introduction.md').read_text().rstrip()
    lines = [intro,'','## Endpoint index','',f'The reference covers **{len(rows)} registered operations**. Metrics availability depends on server configuration.','', '| Method | Path | Purpose |','| --- | --- | --- |']
    for row in rows: lines.append('| '+row['method']+' | [`'+row['path']+'`](#'+anchor(row['method'],row['path'])+') | '+row['summary']+' |')
    for folder in ['Ollama APIs','OpenAI APIs','Management APIs']:
        lines += ['','## '+folder]
        for row in rows:
            if row['contract']['folder'] != folder: continue
            c = row['contract']
            lines += ['', '<a id="'+anchor(row['method'],row['path'])+'"></a>', '', '### '+row['method']+' '+row['path'], '', operation_description(row)]
            params = re.findall(r'\{(\w+)\}',row['path'])
            if params: lines += ['', '**Path parameters (required):** '+', '.join('`'+p+'`' for p in params)+'. URL-encode each value. Use the owning resource\'s identifier; model inspection uses a GUID, while preset name uses a registered preset name.']
            if c['body'] is not None:
                lines += ['','**Example JSON body:**' if not isinstance(c['body'],str) else '**Body variable:** Complete JSON fetched from GET settings.','','```json' if not isinstance(c['body'],str) else '```text',json_text(c['body']),'```']
            # Render request examples using environment references, not real credentials.
            request = request_item(row)['request']
            raw = request['url']['raw'].replace(BASE,'${SHARPAI_URL}')
            raw = re.sub(r'\{\{(\w+)\}\}',lambda m:'${'+re.sub(r'(?<!^)(?=[A-Z])','_',m.group(1)).upper()+'}',raw)
            command = 'curl '+('-I' if row['method']=='HEAD' else '-X '+row['method'])+' "'+raw+'"'
            if request.get('auth',{}).get('type') != 'noauth': command += ' \\\n  -H "Authorization: Bearer ${SHARPAI_BEARER_TOKEN}"'
            if row['path']=='/v1.0/token' and row['method']=='POST': command += ' \\\n  -H "x-email: ${SHARPAI_EMAIL}" \\\n  -H "x-password: ${SHARPAI_PASSWORD}" \\\n  -H "x-tenant-guid: ${SHARPAI_TENANT_GUID:-default}"'
            if c['body'] is not None: command += ' \\\n  -H "Content-Type: application/json" --data-binary @request.json'
            lines += ['', '**Request:**'+(' Save the JSON body above as `request.json` and replace collection placeholders with your values.' if c['body'] is not None else ''),'','```sh',command,'```']
    lines += ['','## Field reference','','These tables list JSON field names, CLR value types, and validation/default documentation from the checked DTO sources. Nullable values may be omitted or null unless an operation requires them. JSON arrays correspond to `List<T>`/array types; enums normally serialize as their named values (defined numeric values are also accepted for settings enums). Operation-specific requirements above take precedence over generic DTO descriptions. Compatibility DTOs can contain accepted but unused fields: consult the compatibility limits before relying on them. Response-only fields (IDs, timestamps, protection flags, computed capabilities) are assigned by the server, not writable inputs.','']
    for schema in types:
        lines += ['### '+schema['name'],'','Source: ['+schema['path']+']('+schema['path']+').','','| JSON field | Type | Meaning / validation |','| --- | --- | --- |']
        for prop in schema['properties']:
            lines.append('| `'+prop['name']+'` | `'+prop['type']+'` | '+prop['description']+' |')
        lines.append('')
    lines += ['## Maintaining this reference','','`docs/api/openapi.json` is the checked snapshot of the server-generated route contract. The generator adds reviewed route-family semantics and DTO field tables from source. After route changes, export the authenticated `/openapi.json` from a server built from the same checkout, update the snapshot, review request/response details in `scripts/generate-rest-reference.py`, and regenerate both artifacts:','','```sh','python3 scripts/generate-rest-reference.py','python3 scripts/generate-rest-reference.py --check','python3 scripts/generate-rest-reference.py --check --live-openapi /path/to/fresh-openapi.json','```','','The checker compares server literal registrations, dynamically registered library routes, the dashboard operation catalog, collection coverage, and generated output. It does not turn a passing documentation check into evidence that every endpoint has been executed. See [Postman usage and validation](postman/README.md).','']
    return '\n'.join(lines)


def validate_collection(rows, generated):
    assert [folder['name'] for folder in generated['item']] == ['Ollama APIs','OpenAI APIs','Management APIs']
    defined = {entry['key'] for entry in generated['variable']}
    used = set(re.findall(r'\{\{(\w+)\}\}',json.dumps(generated)))
    assert used <= defined, 'Undefined collection variables: '+str(used-defined)
    expected = {row['method']+' '+row['path'] for row in rows}
    covered = set()
    identifiers = set()
    def walk(items):
        for item in items:
            if 'item' in item: walk(item['item']); continue
            assert item['id'] not in identifiers, 'Duplicate request ID'
            identifiers.add(item['id'])
            if item['name'] in expected:
                covered.add(item['name'])
                assert any(example['code'] == 200 for example in item['response']), item['name']
                assert item['event'], 'Missing assertions: '+item['name']
            request = item['request']
            if request.get('body',{}).get('raw'):
                raw = request['body']['raw'].replace('{{settingsJson}}','{}').replace('{{maxTokens}}','2048')
                json.loads(raw)
            for example in item.get('response',[]):
                if example.get('body') and any(header.get('value') == 'application/json' for header in example.get('header',[])):
                    json.loads(example['body'])
                    assert '"Password":' not in example['body'], 'Password echoed by an example'
    walk(generated['item'])
    assert covered == expected, 'Incomplete collection: '+str(expected-covered)
    tables = {table['name'] for table in schemas()}
    for row in rows:
        assert set(row['contract']['types']) <= tables, 'Missing field reference: '+row['path']


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check',action='store_true')
    parser.add_argument('--live-openapi',type=pathlib.Path)
    args = parser.parse_args()
    spec = json.loads(SPEC.read_text()); rows = inventory(spec); check_inventory(rows)
    if args.live_openapi:
        live = inventory(json.loads(args.live_openapi.read_text()))
        assert {r['method']+' '+r['path'] for r in live} == {r['method']+' '+r['path'] for r in rows}, 'Live server route inventory differs'
    original = json.loads((ROOT/'postman/SharpAI.postman_collection.json').read_text())
    generated = collection(rows,original)
    validate_collection(rows,generated)
    outputs = {ROOT/'REST_API.md':reference(rows,schemas()), ROOT/'postman/SharpAI.postman_collection.json':json.dumps(generated,indent=2,ensure_ascii=False)+'\n'}
    for path in outputs:
        if args.check:
            assert path.read_text() == outputs[path], 'Stale generated artifact: '+str(path.relative_to(ROOT))
        else: path.write_text(outputs[path])
    print(f'REST reference and Postman coverage verified: {len(rows)} operations; {len(schemas())} field tables')


if __name__ == '__main__':
    main()
