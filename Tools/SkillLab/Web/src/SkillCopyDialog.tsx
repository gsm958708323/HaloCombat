import {useEffect,useState} from 'react';
import {api} from './model';
import type {Project,Skill} from './model';
import {Dialog} from './Dialog';
import {prepareImport} from './authoring/importCandidate';
import type {ImportPayload} from './authoring/importCandidate';
import {otherWorkspace,readDiskProject,workspaceLabel} from './authoring/projectIO';
import type {Workspace} from './authoring/projectIO';
import {planSkillCopy} from './authoring/skillCopy';
import type {CopyDependency,SkillCopyPlan} from './authoring/skillCopy';

const kindLabel:Record<CopyDependency['kind'],string>={
  skills:'技能',timelines:'共享时间轴',projectiles:'共享弹体',areas:'共享区域',buffs:'Buff',summonBlueprints:'召唤蓝图',views:'View',cues:'Cue'
};

export function SkillCopyDialog({workspace,current,apply,close}:{workspace:Workspace;current:()=>Project;
  apply:(before:Project,plan:SkillCopyPlan)=>void;close:()=>void}) {
  const source=otherWorkspace(workspace);
  const [skills,setSkills]=useState<Skill[]>([]);
  const [projectName,setProjectName]=useState('');
  const [selected,setSelected]=useState('');
  const [pending,setPending]=useState<{before:Project;plan:SkillCopyPlan}|null>(null);
  const [loading,setLoading]=useState(true);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  useEffect(()=>{
    let active=true;
    void readDiskProject(source).then(({project})=>{
      if(!active)return;
      setSkills(project.skills);setProjectName(project.name);setSelected(project.skills[0]?.id??'');
    }).catch(error=>{if(active)setError(String(error));}).finally(()=>{if(active)setLoading(false);});
    return ()=>{active=false;};
  },[source]);

  async function preview() {
    setError('');setBusy(true);setPending(null);
    try {
      const response=await api<ImportPayload>(`/project/copy-source?workspace=${source}&skillId=${encodeURIComponent(selected)}`,undefined,'GET');
      const before=current();
      setPending({before,plan:planSkillCopy(before,prepareImport(response),selected)});
    } catch(error){setError(String(error));} finally{setBusy(false);}
  }
  async function confirm() {
    if(!pending)return;
    setError('');setBusy(true);
    try {
      if(current()!==pending.before)throw new Error('目标工程已更改，请重新预览');
      await api('/document/validate',pending.plan.project);
      apply(pending.before,pending.plan);
    } catch(error){setError(String(error));} finally{setBusy(false);}
  }

  const plan=pending?.plan;
  const conflicts=plan?.dependencies.filter(item=>item.resolution==='rename').length??0;
  return <Dialog label="跨工作区复制技能" close={close} busy={busy}>
    <div className="modal skill-copy-dialog">
      <h2>跨工作区复制技能</h2>
      <p>从 {workspaceLabel[source]}{projectName?'（'+projectName+'）':''} 的磁盘版本复制到 {workspaceLabel[workspace]}。</p>
      <label>来源技能<select value={selected} disabled={loading||busy||!skills.length} onChange={event=>{
        setSelected(event.target.value);setPending(null);setError('');
      }}>
        {!skills.length&&<option value="">{loading?'正在读取来源工程…':error?'读取失败':'来源工程没有技能'}</option>}
        {skills.map(skill=><option key={skill.id} value={skill.id}>{skill.name} · {skill.id}</option>)}
      </select></label>
      {plan&&<section className="skill-copy-preview" aria-label="依赖与冲突预览" tabIndex={0}>
        <h3>{plan.dependencies.length} 项内容 · {conflicts} 处 ID 冲突</h3>
        <p>新图节点、连线及运行时身份会重新分配，内部引用同步更新。</p>
        {plan.motorDiffers&&<p className="error">两边的 Motor 设置不同。复制后的技能使用目标工作区的 Motor。</p>}
        <ul>
          {plan.dependencies.map(item=><li key={item.kind+':'+item.from}>
            <strong>{kindLabel[item.kind]} · {item.name}</strong>{item.kind==='skills'&&item.to===plan.root?'（主技能）':''}<br/>
            <code>{item.from}</code>{item.resolution==='rename'?<> → <code>{item.to}</code>（分配新 ID）</>:item.resolution==='reuse'?'（复用）':'（新增）'}
          </li>)}
        </ul>
        {!!plan.detachedReferences.length&&<>
          <h3 className="error">{plan.detachedReferences.length} 处引用将解除绑定</h3>
          <p>来源闭包缺少这些引用的定义。节点会保留，缺失 Cue 的表现节点会禁用。</p>
          <ul>{plan.detachedReferences.map(reference=><li key={reference.path}><code>{reference.path}</code><br/>原 ID <code>{reference.originalId}</code></li>)}</ul>
        </>}
        {!!plan.trackingKeys.length&&<>
          <h3>弹体追踪键冲突</h3>
          <p>复制的弹体和引用它的节点将使用同一个新追踪键。</p>
          <ul>{plan.trackingKeys.map(({from,to})=><li key={from}><code>{from}</code> → <code>{to}</code></li>)}</ul>
        </>}
      </section>}
      {error&&<p className="error" role="alert">{error}</p>}
      <div>
        <button disabled={busy} onClick={close}>取消</button>
        <button disabled={busy||!selected} className={plan?'':'primary'} onClick={()=>void preview()}>{busy&&!plan?'正在读取…':plan?'重新预览':'预览依赖与冲突'}</button>
        {plan&&<button disabled={busy} className="primary" onClick={()=>void confirm()}>{busy?'正在校验…':plan.detachedReferences.length?'解除绑定并复制':'复制技能'}</button>}
      </div>
    </div>
  </Dialog>;
}
