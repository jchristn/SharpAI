import { describe, expect, it } from 'vitest';
import ts from 'typescript';
import en from './en.json';
const files = import.meta.glob('../{app,components}/**/*.tsx', { query:'?raw', import:'default', eager:true }) as Record<string,string>;
function hasKey(key:string): boolean {
  let node: unknown = en;
  for (const part of key.split('.')) { if (!node || typeof node !== 'object') return false; node = (node as Record<string,unknown>)[part]; }
  return typeof node === 'string';
}
describe('source string gates', () => {
  it('keeps visible JSX copy and accessibility labels in catalogs', () => {
    const violations: string[] = [];
    for (const [path,code] of Object.entries(files).filter(([path]) => !path.includes('.test.'))) {
      const source = ts.createSourceFile(path,code,ts.ScriptTarget.Latest,true,ts.ScriptKind.TSX);
      function visit(node:ts.Node):void {
        if (ts.isJsxText(node) && /[A-Za-z]{2}/.test(node.text.trim())) violations.push(`${path}: ${node.text.trim()}`);
        if (ts.isJsxAttribute(node) && ['aria-label','title','placeholder','label','alt'].includes(node.name.getText(source)) && node.initializer && ts.isStringLiteral(node.initializer) && node.initializer.text && node.initializer.text !== 'SharpAI') violations.push(`${path}: ${node.initializer.text}`);
        if (ts.isCallExpression(node) && node.expression.getText(source) === 't' && node.arguments[0] && ts.isStringLiteral(node.arguments[0]) && !hasKey(node.arguments[0].text)) violations.push(`${path}: missing ${node.arguments[0].text}`);
        ts.forEachChild(node,visit);
      }
      visit(source);
    }
    expect(violations).toEqual([]);
  });
});
