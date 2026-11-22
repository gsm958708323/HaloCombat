import {useCallback, useSyncExternalStore} from 'react';
import {TimelineProperties} from './TimelineProperties';
import type {AuthoredTimeline, Project, Skill, TimelineClip} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';

type Props = {project: Project; skill: Skill; store: PreviewRuntimeStore; update: (edit: (p: Project) => Project) => void};
const tracks: {kind: TimelineClip['kind']; label: string}[] = [
  {kind: 'cancel', label: '接续窗口'}, {kind: 'hitbox', label: '攻击判定'}, {kind: 'move', label: '位移'}, {kind: 'iframe', label: '无敌'},
];

export function ComboTimeline({project, skill, store, update}: Props) {
  const shared = project.timelines?.find(timeline => timeline.id === skill.timelineRef);
  const owner = shared ?? skill;
  const timeline = owner.timeline;
  const cast = skill.nodes.find(node => node.kind === 'cast');
  const referenceNames = shared && project.skills.filter(item => item.timelineRef === shared.id).map(item => item.name);
  const changeTimeline = (value: AuthoredTimeline) => update(p => shared
    ? {...p, timelines: p.timelines?.map(item => item.id === shared.id ? {...item, timeline: value} : item)}
    : {...p, skills: p.skills.map(item => item.id === skill.id ? {...item, timeline: value} : item)});
  const changeCast = (field: string, value: number) => update(p => ({...p, skills: p.skills.map(item => item.id !== skill.id ? item : {
    ...item, nodes: item.nodes.map(node => node.id !== cast?.id ? node : {...node, parameters: {...node.parameters, [field]: value}}),
  })}));

  return <section className="combo-timeline" aria-label="技能 Timeline">
    <h3>Timeline · {skill.name}</h3>
    <small>{skill.id}</small>
    {timeline ? <>
      <p>{shared ? <>共享时间轴 · {shared.name}<br/>引用技能（{referenceNames!.length}）· {referenceNames!.join('、')}</> : '本技能时间轴'}</p>
      <TimelineTracks timeline={timeline} skillId={skill.id} store={store}/>
      <TimelineProperties skill={owner} change={changeTimeline} defaultCollapsed/>
    </> : skill.timelineRef ? <p>找不到共享时间轴 · {skill.timelineRef}</p>
      : cast?.parameters.execution === 'operation' ? <p>即时操作，无动作时间轴。</p>
      : cast ? <>
        <p>自动时序 · 由「施放规则」生成时间轴。</p>
        <div className="combo-cast-times">
          {[{key: 'cancelStart', label: '接续窗口开始 / 秒'}, {key: 'cancelEnd', label: '接续窗口结束 / 秒（0 关闭）'}].map(field =>
            <label key={field.key}>{field.label}
              <input type="number" min="0" max="120" step=".02" value={Number(cast.parameters[field.key] ?? 0)} onChange={event => {
                const value = event.target.valueAsNumber;
                if (Number.isFinite(value)) changeCast(field.key, value);
              }}/>
            </label>)}
        </div>
      </> : <p>没有可用的施放规则。</p>}
  </section>;
}

function TimelineTracks({timeline, skillId, store}: {timeline: AuthoredTimeline; skillId: string; store: PreviewRuntimeStore}) {
  const getTime = useCallback(() => {
    const caster = store.get().state?.actors.find(actor => actor.kind === 'caster');
    return caster?.currentSkill === skillId ? caster.actionTime : null;
  }, [store, skillId]);
  const time = useSyncExternalStore(store.subscribe, getTime);
  const percent = (value: number) => `${Math.max(0, Math.min(100, value * 100 / Math.max(.001, timeline.duration)))}%`;
  const payloadLanes = new Map<number, number>();
  return <div className="combo-tracks" aria-label="技能时间轴轨道">
    <div className="combo-track"><span>动作秒</span><span className="combo-track-ruler"><span>0</span>
      {time !== null && <span>当前 {time.toFixed(2)}</span>}<span>{timeline.duration}</span></span></div>
    {tracks.map(({kind, label}) => <div key={kind} className="combo-track"><span>{label}</span><div>
      {timeline.clips.filter(clip => clip.kind === kind).map(clip => <i key={clip.id}
        style={{left: percent(clip.start), width: percent(clip.end - clip.start)}} title={`${clip.start}–${clip.end} 秒`}/>)}
      {time !== null && <b style={{left: percent(time)}}/>}
    </div></div>)}
    <div className="combo-track"><span>效果触发</span><div>
      {timeline.payloads.map(payload => {
        const lane = payloadLanes.get(payload.time) ?? 0;
        payloadLanes.set(payload.time, lane + 1);
        return <i key={payload.id} className="combo-payload-marker" style={{left: percent(payload.time), top: lane % 3 * 3}}
          title={`${payload.id} · ${payload.time} 秒`}/>;
      })}
      {time !== null && <b style={{left: percent(time)}}/>}
    </div></div>
  </div>;
}
