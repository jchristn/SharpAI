import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { CopyButton } from './CopyButton';
import { copyToClipboard } from './clipboard';

afterEach(()=>{vi.restoreAllMocks();});
describe('clipboard controls',()=>{
 it('copies securely and confirms with an icon',async()=>{
  const writeText=vi.fn().mockResolvedValue(undefined);
  Object.defineProperty(navigator,'clipboard',{configurable:true,get:()=>({writeText})});
  render(<CopyButton value="secret"/>);
  const button=screen.getByRole('button',{name:'Copy'});
  expect(button.textContent).toBe('');
  fireEvent.click(button);
  await waitFor(()=>expect(button).toHaveAccessibleName('Copied'));
  expect(writeText).toHaveBeenCalledWith('secret');
  expect(button).toHaveClass('sa-copy-button--copied');
 });
 it('uses a temporary selected field on insecure origins and restores focus',async()=>{
  Object.defineProperty(navigator,'clipboard',{configurable:true,get:()=>undefined});
  const copy=vi.fn(()=>true);
  Object.defineProperty(document,'execCommand',{configurable:true,value:copy});
  const input=document.createElement('input');document.body.append(input);input.focus();
  expect(await copyToClipboard('http copy')).toBe(true);
  expect(copy).toHaveBeenCalledWith('copy');
  expect(document.activeElement).toBe(input);
  expect(document.querySelector('textarea')).toBeNull();input.remove();
 });
 it('falls back if clipboard permission is rejected and never reports false success',async()=>{
  Object.defineProperty(navigator,'clipboard',{configurable:true,get:()=>({writeText:vi.fn().mockRejectedValue(new Error('denied'))})});
  Object.defineProperty(document,'execCommand',{configurable:true,value:vi.fn(()=>false)});
  expect(await copyToClipboard('value')).toBe(false);
 });
});
