import type { Project,RuntimeView } from './model';
import './views.css';

export function ViewProperties({project,update}:{project:Project;update:(change:(p:Project)=>Project)=>void}) {
  const runtime=project.runtime;if(!runtime)return null;
  const views=runtime.views??[];
  const change=(id:string,patch:Partial<RuntimeView>)=>update(p=>({...p,runtime:{...p.runtime!,views:(p.runtime?.views??[]).map(v=>v.id===id?{...v,...patch}:v)}}));
  return <details className="view-properties"><summary>角色与动画资源 · {views.length}</summary>
    <p>资源键为 Unity Resources 内路径。网页显示逻辑示意；发布后在 Unity 检查真实 Prefab、Animator 和动作相位。</p>
    {runtime.actors.map(a=><label key={a.id}>{a.id}<select aria-label={`角色${a.id}视图`} value={a.view} onChange={e=>update(p=>({...p,runtime:{...p.runtime!,actors:p.runtime!.actors.map(v=>v.id===a.id?{...v,view:e.target.value}:v)}}))}>
      {[...['fighter','stake','melee_ai','melee_guard','ranged_ai','summon'],...views.filter(v=>v.kind!=='body').map(v=>v.id)].map(key=><option key={key} value={key}>{key}</option>)}
    </select></label>)}
    {views.map(view=><details key={view.id}><summary>{view.id}</summary>
      <label>Prefab 资源键<input aria-label={`${view.id}资源键`} value={view.resource} onChange={e=>change(view.id,{resource:e.target.value})}/></label>
      <small>{view.kind==='body'?'实体外观：无角色 Animator，旋转与粒子跟随逻辑时钟。':'角色外观：Animator由时间轴控制。'}</small>
      {(['scale','yawOffset'] as const).map(key=><label key={key}>{key==='scale'?'模型缩放':'局部朝向 (度)'}<input aria-label={`${view.id}${key}`} type="number" step=".01" value={view[key]} onChange={e=>{if(e.target.value!=='')change(view.id,{[key]:Number(e.target.value)});}}/></label>)}
      {view.kind==='body'?<label>旋转速度 (度/秒)<input aria-label={`${view.id}旋转速度`} type="number" value={view.spinSpeed??0} onChange={e=>{if(e.target.value!=='')change(view.id,{spinSpeed:Number(e.target.value)});}}/></label>:<small>语义键 → Animator 第 0 层完整状态路径</small>}
      {Object.entries(view.states).map(([key,state])=><label key={key}>{key}<input aria-label={`${view.id}状态${key}`} value={state} onChange={e=>change(view.id,{states:{...view.states,[key]:e.target.value}})}/></label>)}
    </details>)}
    <small>新增目录或语义键可在“正式运行内容”中编辑。缺失资源/状态会拒绝 Unity 重载并保留当前局。</small>
  </details>;
}
