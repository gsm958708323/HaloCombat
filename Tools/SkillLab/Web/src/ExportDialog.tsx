import {useState} from 'react';
import {api} from './model';
import type {Project} from './model';
import {Dialog} from './Dialog';

interface Exported {filename:string;json:string;revision:string;skills:number;buffs:number;projectiles:number;areas:number;actors:number}
export function ExportDialog({project,skillId,close,report}:{project:Project;skillId:string;close:()=>void;report:(text:string)=>void}) {
  const [kind,setKind]=useState('project'),[busy,setBusy]=useState(false),[error,setError]=useState(''),[result,setResult]=useState<Exported|null>(null);
  const skill=project.skills.find(s=>s.id===skillId);
  async function download(){
    setBusy(true);setError('');
    try{
      const value=await api<Exported>('/export/'+kind+(kind==='skill'?'?skillId='+encodeURIComponent(skillId):''),project);
      const url=URL.createObjectURL(new Blob([value.json],{type:'application/json'})),anchor=document.createElement('a');
      anchor.href=url;anchor.download=value.filename;anchor.click();URL.revokeObjectURL(url);setResult(value);report('已导出 '+value.filename+' · '+value.revision.slice(0,8));
    }catch(error){setError(String(error));}finally{setBusy(false);}
  }
  return <Dialog label="导出 JSON" close={close} busy={busy}><form className="modal content-dialog" onSubmit={e=>{e.preventDefault();void download();}}>
    <h2>导出 JSON</h2>
    <label>导出类型<select aria-label="导出类型" value={kind} disabled={busy} onChange={e=>{setKind(e.target.value);setResult(null);setError('');}}>
      <option value="project">完整工程（保留编辑布局）</option><option value="skill" disabled={!skill}>当前技能运行包（包含依赖）</option><option value="runtime">完整运行内容（全部技能）</option>
    </select></label>
    <p>{kind==='project'?'保存所有技能、资源引用、场景布置与编辑状态。空工程和暂未连好的草稿也能导出。':kind==='skill'?'导出'+(skill?.name??'当前技能')+'及实际依赖，可作为SkillLab的F6/F7文件。完整校验通过后才能导出。':'导出全部技能、共享定义和正式运行配置。省略编辑状态与节点位置，可交给游戏运行，也能重新导入编辑。需要角色定义，示例工程请先打开正式内容或导出单技能包。'}</p>
    <p>文件为 JSON；Unity Prefab、贴图与音频通过资源键引用，资源文件随 Unity 项目交付。</p>
    {result&&<section role="status"><h3>已导出 {result.filename}</h3><p>{result.skills} 技能 · {result.buffs} Buff · {result.projectiles} 弹体定义 · {result.areas} 区域定义 · {result.actors} 角色</p><small>版本 {result.revision.slice(0,12)}</small></section>}
    {error&&<p role="alert">{error}</p>}
    <div><button type="button" disabled={busy} onClick={close}>关闭</button><button className="primary" disabled={busy}>{busy?'正在校验…':'导出文件'}</button></div>
  </form></Dialog>;
}
