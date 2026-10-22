/** Secure-context clipboard API with a synchronous fallback for HTTP origins. */
export async function copyToClipboard(value: string): Promise<boolean> {
  const fallback = (): boolean => {
    const focused = document.activeElement as HTMLElement | null;
    const selection = window.getSelection();
    const ranges = selection ? Array.from({length: selection.rangeCount}, (_, i) => selection.getRangeAt(i).cloneRange()) : [];
    const field = document.createElement('textarea');
    field.value = value;
    field.style.cssText = 'position:fixed;top:0;left:-9999px;opacity:0';
    document.body.appendChild(field);
    field.select();
    try { return document.execCommand('copy'); }
    catch { return false; }
    finally {
      field.remove(); focused?.focus({preventScroll:true});
      if(selection) { selection.removeAllRanges(); ranges.forEach(range => selection.addRange(range)); }
    }
  };
  if (!navigator.clipboard?.writeText) return fallback();
  try { await navigator.clipboard.writeText(value); return true; }
  catch { return fallback(); }
}
