/** Help is assigned by configuration contract, never by fuzzy matching translated labels. */
export function settingHelpKey(path:string):string {
  const parts=path.split('.');
  const key=parts[parts.length-1] ?? '';
  if(['CreatedUtc','SchemaVersion','SoftwareVersion','Prefix','CacheCount'].includes(key))return 'readonly';
  const exact:Record<string,string>={
    'Auth.Enabled':'authEnable','RequestHistory.Enabled':'historyEnable',
    'Runtime.EnableNativeLogging':'nativeLogging','Logging.ConsoleLogging':'console','Logging.EnableColors':'colors',
    'Telemetry.EnableMetrics':'metrics','Telemetry.EnableTraces':'traces','Telemetry.EnableLogs':'logs',
    'Rest.Ssl.Enable':'ssl','Rest.Ssl.MutuallyAuthenticate':'mutualTls',
    'Rest.Headers.IncludeContentLength':'headersLength','Rest.IO.EnableKeepAlive':'keepAlive',
    'Rest.AccessControl.DenyList':'accessList','Rest.AccessControl.PermitList':'accessList',
  };
  if(exact[path])return exact[path];
  if(/^EnableHttp[123]/.test(key))return 'http';
  if(path==='Database.Type')return 'database';
  if(path==='Database.Filename')return 'sqlite';
  if(path==='Database.Port')return 'databasePort';
  if(path==='Logging.MinimumSeverity')return 'severity';
  if(path==='Runtime.ForceBackend')return 'backend';
  if(path==='Telemetry.OtlpProtocol')return 'protocol';
  if(path==='Telemetry.OtlpEndpoint')return 'collector';
  if(path==='Rest.AccessControl.Mode')return 'accessMode';
  if(path==='HuggingFace.ApiKey')return 'huggingFace';
  if(path==='Auth.TokenSigningKey')return 'signing';
  if(path==='Auth.AdminApiKeys')return 'adminKeys';
  if(path==='Auth.SessionTtlMinutes')return 'session';
  if(path==='RequestHistory.RetentionDays')return 'retention';
  if(path==='RequestHistory.MaxRequestBodyBytes'||path==='RequestHistory.MaxResponseBodyBytes')return 'capture';
  if(path==='Logging.Servers')return 'syslog';
  if(path.startsWith('Logging.Servers.'))return ({Hostname:'syslogHost',Port:'syslogPort',RandomizePorts:'randomize',MinimumPort:'minPort',MaximumPort:'maxPort'} as Record<string,string>)[key] ?? 'syslog';
  if(key==='DefaultHeaders')return 'headers';
  if(key==='QuantizationPriority')return 'quantizationRank';
  if(key.endsWith('Directory'))return 'directory';
  if(key.endsWith('Filename'))return 'filename';
  if(key.endsWith('BackendPath'))return 'library';
  if(key.endsWith('Ms'))return 'milliseconds';
  if(key.endsWith('Seconds'))return 'seconds';
  if(key.includes('Size')||key.endsWith('Bytes'))return 'bytes';
  if(key.includes('Password'))return 'password';
  if(key==='Hostname'||key==='PrometheusHostname')return 'hostname';
  if(key==='Port'||key==='PrometheusPort')return 'port';
  return 'configuration';
}
