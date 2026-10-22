/** Accept a repository ID or a validated Hugging Face URL; preserve revision and exact GGUF selection. */
export function normalizeRepository(input: string): string {
  const name = input.trim().replace(/\/$/, '');
  if (/^https?:\/\//i.test(name)) {
    let url: URL;
    try { url = new URL(name); } catch { throw new Error('models.invalidUrl'); }
    if (url.protocol !== 'https:' || !['huggingface.co', 'www.huggingface.co', 'hf.co'].includes(url.hostname)
      || url.port || url.username || url.password || url.search || url.hash || name.includes('\\')
      || /\/(?:\.|%2e){1,2}(?:\/|$)/i.test(name)) throw new Error('models.invalidUrl');
    let parts: string[];
    try { parts = url.pathname.replace(/^\/|\/$/g, '').split('/').map(decodeURIComponent); } catch { throw new Error('models.invalidUrl'); }
    if (parts.some(part => !part || part === '.' || part === '..' || part.includes('\\'))
      || !/^[A-Za-z0-9_][A-Za-z0-9_.-]*\/[A-Za-z0-9_][A-Za-z0-9_.-]*$/.test(parts.slice(0, 2).join('/'))) throw new Error('models.invalidRepository');
    if (parts.length === 2) return parts.join('/');
    if (parts[2] === 'tree' && parts.length === 4) return url.toString();
    if (['resolve', 'blob'].includes(parts[2]) && parts.length >= 5 && parts[parts.length - 1].toLowerCase().endsWith('.gguf') && !parts.slice(4).some(part => part.includes('/'))) return url.toString();
    throw new Error('models.invalidUrl');
  }
  if (!/^[A-Za-z0-9_][A-Za-z0-9_.-]*\/[A-Za-z0-9_][A-Za-z0-9_.-]*$/.test(name)) throw new Error('models.invalidRepository');
  return name;
}
