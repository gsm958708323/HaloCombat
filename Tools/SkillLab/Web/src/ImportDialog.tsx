import {useRef,useState} from 'react';
import {api} from './model';
import {Dialog} from './Dialog';
import {prepareImport} from './authoring/importCandidate';
import type {ImportCandidate,ImportPayload} from './authoring/importCandidate';

export function ImportDialog({close,ready}:{close:()=>void;ready:(candidate:ImportCandidate)=>void}) {
  const [text,setText]=useState(''),[error,setError]=useState(''),[busy,setBusy]=useState(false);
  const input=useRef<HTMLInputElement>(null);
  async function select(file:File){
    setBusy(true);setError('');
    try{setText(await file.text());}catch(error){setError('无法读取文件：'+String(error));}finally{setBusy(false);}
  }
  async function parse(){
    setBusy(true);setError('');
    try{
      const result=await api<ImportPayload>('/document/import?detachMissingReferences=preview',{text});
      ready(prepareImport(result));
    }catch(error){setError(String(error));}finally{setBusy(false);}
  }
  return <Dialog label="导入 JSON" close={close} busy={busy}><form className="modal import-dialog" onSubmit={event=>{event.preventDefault();void parse();}}>
    <h2>导入 JSON</h2><p>选择工程或运行包文件，也可以粘贴原始 JSON。校验后预览内容，再选择替换或合并。</p>
    <button type="button" disabled={busy} onClick={()=>input.current?.click()}>选择 JSON 文件</button>
    <input type="file" ref={input} accept=".json,application/json" hidden onChange={event=>{const file=event.target.files?.[0];if(file)void select(file);event.target.value='';}}/>
    <textarea aria-label="导入 JSON 文本" autoFocus spellCheck={false} value={text} disabled={busy} onChange={e=>setText(e.target.value)}/>
    {error&&<p className="error" role="alert">{error}</p>}
    <div><button type="button" disabled={busy} onClick={close}>取消</button><button className="primary" disabled={busy||!text.trim()}>{busy?'正在校验…':'解析并预览'}</button></div>
  </form></Dialog>;
}
