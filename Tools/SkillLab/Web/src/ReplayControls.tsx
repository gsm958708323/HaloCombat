import {useRef,useState} from 'react';
import type {Project} from './model';
import type {usePreview} from './usePreview';
import './replay.css';

export function ReplayControls({preview,project,update}:{preview:ReturnType<typeof usePreview>;project:Project;update:(p:Project)=>void}) {
  const input=useRef<HTMLInputElement>(null),[error,setError]=useState('');
  async function replay(){setError('');try{await preview.replayFile();}catch(error){setError(String(error));}}
  async function importFile(file:File){setError('');try{await preview.replayFile(await file.text());}catch(error){setError(String(error));}}
  return <details className="replay-controls"><summary>输入记录与复现 · {preview.recordInputs?(preview.recording?.inputs??0)+' 次':'未开启'}</summary>
    <label><input aria-label="开启输入记录" type="checkbox" checked={preview.recordInputs} disabled={preview.replaying||preview.placing} onChange={e=>preview.setRecordInputs(e.target.checked)}/>开启输入记录（切换会重置预览）</label>
    <label>随机种子<input aria-label="预览随机种子" type="number" min="-2147483648" max="2147483647" step="1" value={project.preview.seed} disabled={preview.replaying} onChange={e=>{if(e.target.value==='')return;const seed=Number(e.target.value);if(!Number.isInteger(seed)||seed< -2147483648||seed>2147483647){setError('种子须为32位整数');return;}setError('');update({...project,preview:{...project.preview,seed}});}}/></label>
    <small>按0.02秒固定步记录输入；重放停在记录末步。配置与种子随文件保存，导入不会写磁盘工程。</small>
    <div><button disabled={!preview.ready||!preview.recording?.enabled||!!preview.recording?.error} onClick={()=>void replay()}>{preview.replaying?'正在处理…':'重放本次'}</button>
    <button disabled={!preview.ready||!preview.recording?.enabled||!!preview.recording?.error} onClick={()=>void preview.exportReplay().catch(error=>setError(String(error)))}>导出输入记录</button>
    <button disabled={preview.replaying} onClick={()=>input.current?.click()}>导入并复现</button></div>
    <input ref={input} type="file" accept=".json,application/json" hidden onChange={e=>{const file=e.target.files?.[0];if(file)void importFile(file);e.target.value='';}}/>
    {preview.recording?.error&&<p role="alert">{preview.recording.error}</p>}{error&&<p role="alert">{error}</p>}
    {preview.comparison&&<p role="status">复现一致 · {preview.recording?.steps}步 · {preview.comparison.values}个值 · 最大差 {preview.comparison.maximumNumericDifference.toExponential(2)}（容差{preview.comparison.tolerance}）</p>}
  </details>;
}
