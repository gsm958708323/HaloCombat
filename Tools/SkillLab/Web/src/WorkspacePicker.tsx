import {useState} from 'react';
import {Dialog} from './Dialog';
import {workspaceLabel} from './authoring/projectIO';
import type {Workspace} from './authoring/projectIO';
import type {useProjectDocument} from './useProjectDocument';

export function WorkspacePicker({editor,report,disabled}:{editor:ReturnType<typeof useProjectDocument>;report:(text:string)=>void;disabled:boolean}) {
  const [target,setTarget]=useState<Workspace|null>(null);
  const [error,setError]=useState('');
  const busy=editor.loading||editor.saving||editor.publishing;
  async function open(workspace:Workspace,mode:'clean'|'save'|'discard') {
    setError('');
    try { await editor.load(workspace,mode);setTarget(null); }
    catch (error) {
      if(mode==='clean')report(String(error));
      else setError(String(error));
    }
  }
  function choose(workspace:Workspace) {
    if(workspace===editor.workspace)return;
    if(editor.dirty){setError('');setTarget(workspace);}
    else void open(workspace,'clean');
  }
  return <>
    <select aria-label="工作区" value={editor.workspace} disabled={busy||disabled||!!target} onChange={e=>choose(e.target.value as Workspace)}>
      <option value="arena">{workspaceLabel.arena}</option>
      <option value="lab">{workspaceLabel.lab}</option>
    </select>
    {target&&<Dialog label="切换工作区" close={()=>setTarget(null)} busy={busy}>
      <div className="modal">
        <h2>切换到 {workspaceLabel[target]}</h2>
        <p>当前工程有未保存的修改。保存当前工程后切换，或放弃修改。</p>
        {error&&<p className="error" role="alert">{error}</p>}
        <div>
          <button disabled={busy} onClick={()=>setTarget(null)}>取消</button>
          <button disabled={busy} onClick={()=>void open(target,'discard')}>放弃修改并切换</button>
          <button disabled={busy} className="primary" onClick={()=>void open(target,'save')}>保存并切换</button>
        </div>
      </div>
    </Dialog>}
  </>;
}
