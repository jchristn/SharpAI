import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../../api/ApiClient';
import { ApiContext } from '../ApiProvider';
import { ModelsView } from './ModelsView';

describe('structured model pull',()=>{
  it('renders intermediate bytes and percentage, then a completed state without raw protocol output',async()=>{
    const api=new ApiClient('');
    vi.spyOn(api,'listModels').mockResolvedValue({models:[]});
    vi.spyOn(api,'runningModels').mockResolvedValue({models:[]});
    let report: (line:string)=>void=()=>{};
    let finish: ()=>void=()=>{};
    vi.spyOn(api,'pullModel').mockImplementation(async(_name,onLine)=>{report=onLine;await new Promise<void>(resolve=>{finish=resolve;});});
    const {container}=render(<ApiContext.Provider value={api}><ModelsView/></ApiContext.Provider>);
    fireEvent.click(screen.getByRole('button',{name:'Pull model'}));
    fireEvent.change(screen.getByRole('textbox',{name:'Model name or full URL'}),{target:{value:'org/model'}});
    fireEvent.click(screen.getByRole('dialog').querySelector('.sa-btn--primary')!);
    await waitFor(()=>expect(api.pullModel).toHaveBeenCalled());
    act(()=>report(JSON.stringify({status:'downloading',downloaded:1048576,percent:.5,file:'model.gguf'})));
    expect(screen.getByRole('progressbar')).toHaveAttribute('value','0.5');
    expect(screen.getByText('50%')).toBeVisible();
    expect(screen.getByText('1 MB')).toBeVisible();
    expect(screen.getByText('model.gguf')).toBeVisible();
    expect(container.querySelector('pre')).toBeNull();
    await act(async()=>{report('{"status":"success","percent":1}');finish();});
    expect(screen.getByText('Model ready')).toBeVisible();
  });
});
