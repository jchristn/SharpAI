/** Accept a Hugging Face repository ID or repository-page URL; never send arbitrary URLs upstream. */
export function normalizeRepository(input: string): string {
  let name = input.trim();
  if (/^https?:\/\//i.test(name)) {
    let url: URL;
    try { url = new URL(name); } catch { throw new Error('models.invalidUrl'); }
    if (url.protocol !== 'https:' || !['huggingface.co','www.huggingface.co','hf.co'].includes(url.hostname)) throw new Error('models.invalidUrl');
    const parts = url.pathname.split('/').filter(Boolean);
    if (parts.length > 2 && !(parts[2] === 'tree' && parts[3] === 'main' && parts.length === 4)) throw new Error('models.invalidUrl');
    name = parts.slice(0,2).join('/');
  }
  name = name.replace(/\/$/,'');
  if (!/^[\w.-]+\/[\w.-]+$/.test(name) || name.split('/').some(part => part === '.' || part === '..')) throw new Error('models.invalidRepository');
  return name;
}
