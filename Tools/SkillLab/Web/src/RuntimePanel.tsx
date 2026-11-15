import {useEffect,useState} from 'react';
import type {AuthorNodeSource,PreviewState} from './model';
import {runtimeEventFilters,runtimeEventView} from './authoring/runtimeEvents';
import {RuntimeEventTable} from './RuntimeEventTable';
import {RuntimeInstances} from './RuntimeInstances';
import {DamageInspector} from './DamageInspector';

export function RuntimePanel({state,sessionId,ready,inspectSource,inspectEntity,remove,removeBuff}:{state:PreviewState|null;sessionId:string;ready:boolean;inspectSource:(source:AuthorNodeSource)=>void;inspectEntity:(id:string)=>void;remove:(id:string)=>Promise<unknown>;removeBuff:(id:string,instance:number)=>Promise<unknown>}) {
  const [filter,setFilter]=useState('key'),[instances,setInstances]=useState(false),[since,setSince]=useState(0);
  useEffect(()=>setSince(0),[sessionId]);
  const view=runtimeEventView(state?.events??[],filter,since);
  function clear(){setSince(Math.max(0,...(state?.events??[]).map(e=>e.sequence)));}
  return <section className="events-panel"><div className="panel-title"><div className="text-tabs"><button className={!instances?'active':''} onClick={()=>setInstances(false)}>事件记录</button><button className={instances?'active':''} onClick={()=>setInstances(true)}>运行实例</button></div>
    {!instances&&<select aria-label="筛选事件" value={filter} onChange={e=>setFilter(e.target.value)}>{runtimeEventFilters.map(([key,label])=><option value={key} key={key}>{label}</option>)}</select>}
    <small aria-label={instances?'运行实例数量':'事件数量'} title={instances?'当前存活实体':'筛选后 / 保留记录；最多保留500条'}>{instances?(state?.actors.length??0)+' 个':view.total+' / '+view.retained.length+' 条'}</small>
    {!instances&&<button className="clear-events" aria-label="清空显示的事件" title="清空显示；伤害统计和输入记录继续保留" disabled={!view.retained.length} onClick={clear}>清空</button>}
  </div><div className="event-scroll">{instances?<RuntimeInstances actors={state?.actors??[]} ready={ready} inspectSource={inspectSource} inspectEntity={inspectEntity} remove={remove} removeBuff={removeBuff}/>:<>
    <DamageInspector events={view.retained}/>{view.hidden>0&&<p className="runtime-empty">显示最近{view.rows.length}条，另有{view.hidden}条。</p>}
    <RuntimeEventTable events={view.rows} inspectSource={inspectSource} inspectEntity={inspectEntity} ready={ready}/>{!view.rows.length&&<p className="runtime-empty">当前筛选没有事件。</p>}
  </>}</div></section>;
}
