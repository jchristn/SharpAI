/** Filled brand silhouettes and clean theme geometry rendered at their native 24px grid. */
export function HeaderIcon({name}:{name:'github'|'discord'|'sun'|'moon'}):JSX.Element {
 const paths={
  github:<path d="M12 .3a12 12 0 0 0-3.8 23.4c.6.1.8-.3.8-.6v-2.2c-3.3.7-4-1.4-4-1.4-.5-1.4-1.3-1.8-1.3-1.8-1.1-.8.1-.8.1-.8 1.2.1 1.8 1.2 1.8 1.2 1.1 1.8 2.8 1.3 3.5 1 .1-.8.4-1.3.8-1.6-2.7-.3-5.5-1.4-5.5-6A4.7 4.7 0 0 1 5.6 8c-.1-.3-.5-1.6.1-3.4 0 0 1-.3 3.3 1.2a11.5 11.5 0 0 1 6 0c2.3-1.5 3.3-1.2 3.3-1.2.6 1.8.2 3.1.1 3.4a4.7 4.7 0 0 1 1.2 3.3c0 4.6-2.8 5.7-5.5 6 .4.4.8 1.1.8 2.2v3.5c0 .3.2.7.8.6A12 12 0 0 0 12 .3Z"/>,
  discord:<path d="M20.3 4.4a20 20 0 0 0-4.9-1.5l-.6 1.2a18 18 0 0 0-5.5 0l-.6-1.2a20 20 0 0 0-4.9 1.5C.5 9-.3 13.6.1 18.1a20 20 0 0 0 6 3l1.2-2a13 13 0 0 1-1.9-.9l.4-.3a16 16 0 0 0 12.1 0l.4.3-1.9.9 1.2 2a20 20 0 0 0 6-3c.5-5.2-.8-9.7-3.3-13.7ZM8 15.3c-1.2 0-2.2-1.1-2.2-2.4s1-2.4 2.2-2.4 2.2 1.1 2.2 2.4-1 2.4-2.2 2.4Zm8 0c-1.2 0-2.2-1.1-2.2-2.4s1-2.4 2.2-2.4 2.2 1.1 2.2 2.4-1 2.4-2.2 2.4Z"/>,
  moon:<path d="M21 13a9 9 0 0 1-10-10A9 9 0 1 0 21 13Z"/>,
  sun:<><circle cx="12" cy="12" r="5"/><path d="M12 2v2m0 16v2M2 12h2m16 0h2M5 5l1 1m12 12 1 1M5 19l1-1M18 6l1-1" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/></>
 };
 return <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor" shapeRendering="geometricPrecision" aria-hidden="true">{paths[name]}</svg>;
}
