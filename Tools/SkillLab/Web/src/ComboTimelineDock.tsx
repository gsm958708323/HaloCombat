import {useCallback, useMemo, useRef, useState, useSyncExternalStore} from 'react';
import {ChevronDown, ChevronUp, Plus, SlidersHorizontal, Sparkles} from 'lucide-react';
import type {AuthoredTimeline, Project, RuntimeCombo, Skill, TimelineClip} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';
import {CANCEL_CHANNELS, channelColor, inputColor, inputShortLabel, skillMeta} from './authoring/comboModel';
import {TimelineProperties} from './TimelineProperties';

type Props = {
  project: Project;
  skill?: Skill;
  selectedRule?: RuntimeCombo;
  rules: readonly RuntimeCombo[];
  store: PreviewRuntimeStore;
  update: (edit: (p: Project) => Project, remember?: boolean, group?: string) => void;
  onSelectRule: (ruleId: string) => void;
};

type DragState = {
  kind: 'clip' | 'auto-cancel' | 'edge-range' | 'payload';
  mode: 'move' | 'start' | 'end';
  id: string;
  originX: number;
  trackWidth: number;
  initialStart: number;
  initialEnd: number;
  duration: number;
};

const FRAME = 0.02;
const snap = (value: number) => Math.round(value / FRAME) * FRAME;
const clamp = (value: number, min: number, max: number) => Math.max(min, Math.min(max, value));
const toSec = (value: number) => Number(value.toFixed(2));

export function ComboTimelineDock({project, skill, selectedRule, rules, store, update, onSelectRule}: Props) {
  const [collapsed, setCollapsed] = useState(false);
  const [height, setHeight] = useState(215);
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [addingChannel, setAddingChannel] = useState(false);
  const [dragLabel, setDragLabel] = useState<string | null>(null);
  const dragRef = useRef<DragState | null>(null);

  const getTime = useCallback(() => {
    if (!skill) return null;
    const caster = store.get().state?.actors.find(a => a.kind === 'caster');
    return caster?.currentSkill === skill.id ? caster.actionTime : null;
  }, [store, skill]);
  const liveTime = useSyncExternalStore(store.subscribe, getTime);

  const meta = useMemo(() => (skill ? skillMeta(project, skill) : null), [project, skill]);
  const shared = useMemo(() => (skill?.timelineRef ? project.timelines?.find(t => t.id === skill.timelineRef) : undefined), [project.timelines, skill]);
  const owner = shared ?? skill;
  const timeline = owner?.timeline;
  const castNode = skill?.nodes.find(n => n.kind === 'cast');
  const duration = Math.max(0.02, meta?.duration ?? 0.5);

  const customRangeEdges = useMemo(() => {
    if (!skill) return [];
    return rules.filter(r => r.preSkills.includes(skill.id) && r.window?.mode === 'range');
  }, [rules, skill]);

  const changeTimeline = useCallback((nextTimeline: AuthoredTimeline, group = '') => {
    if (!skill) return;
    update(p => shared
      ? {...p, timelines: p.timelines?.map(t => t.id === shared.id ? {...t, timeline: nextTimeline} : t)}
      : {...p, skills: p.skills.map(s => s.id === skill.id ? {...s, timeline: nextTimeline} : s)}, true, group);
  }, [skill, shared, update]);

  const changeAutoCancel = useCallback((start: number, end: number, group = '') => {
    if (!skill || !castNode) return;
    update(p => ({
      ...p,
      skills: p.skills.map(s => s.id !== skill.id ? s : {
        ...s,
        nodes: s.nodes.map(n => n.id !== castNode.id ? n : {
          ...n,
          parameters: {...n.parameters, cancelStart: toSec(start), cancelEnd: toSec(end)},
        }),
      }),
    }), true, group);
  }, [skill, castNode, update]);

  const changeEdgeRange = useCallback((ruleId: string, start: number, end: number, group = '') => {
    update(p => {
      if (!p.runtime) return p;
      return {
        ...p,
        runtime: {
          ...p.runtime,
          combos: p.runtime.combos.map(r => r.id !== ruleId ? r : {
            ...r,
            window: {mode: 'range', start: toSec(start), end: toSec(end)},
          }),
        },
      };
    }, true, group);
  }, [update]);

  const upgradeToExplicitTimeline = () => {
    if (!skill || !castNode || timeline) return;
    const dur = Math.max(0.04, Number(castNode.parameters.duration ?? 0.4));
    const cStart = Number(castNode.parameters.cancelStart ?? 0);
    const cEnd = Number(castNode.parameters.cancelEnd ?? 0);
    const clips: TimelineClip[] = [];
    if (cEnd > cStart) clips.push({id: crypto.randomUUID(), kind: 'cancel', start: toSec(cStart), end: toSec(Math.min(dur, cEnd)), effects: []});
    const nextTimeline: AuthoredTimeline = {
      runtimeId: skill.runtimeId,
      duration: toSec(dur),
      clips,
      payloads: [],
      controlTags: [],
      scaleWithActionSpeed: false,
    };
    changeTimeline(nextTimeline);
  };

  const addCancelClip = (channel?: string) => {
    if (!timeline) return;
    const start = toSec(Math.min(duration * 0.25, Math.max(0, duration - 0.1)));
    const end = toSec(Math.min(duration, Math.max(start + 0.08, duration * 0.85)));
    const next: TimelineClip = {
      id: crypto.randomUUID(),
      kind: 'cancel',
      start,
      end,
      ...(channel ? {channel} : {}),
      effects: [],
    };
    changeTimeline({...timeline, clips: [...timeline.clips, next]});
    setAddingChannel(false);
  };

  function beginDrag(
    event: React.PointerEvent<HTMLElement>,
    kind: DragState['kind'],
    mode: DragState['mode'],
    id: string,
    initialStart: number,
    initialEnd: number,
  ) {
    event.stopPropagation();
    const trackEl = event.currentTarget.closest('.ctl-lane-track') as HTMLElement | null;
    if (!trackEl) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    dragRef.current = {
      kind, mode, id,
      originX: event.clientX,
      trackWidth: Math.max(1, trackEl.getBoundingClientRect().width),
      initialStart, initialEnd, duration,
    };
  }

  function moveDrag(event: React.PointerEvent<HTMLElement>) {
    const d = dragRef.current;
    if (!d) return;
    const deltaSec = ((event.clientX - d.originX) / d.trackWidth) * d.duration;
    let nextStart = d.initialStart;
    let nextEnd = d.initialEnd;
    if (d.kind === 'payload') {
      nextStart = clamp(snap(d.initialStart + deltaSec), 0, d.duration);
      setDragLabel(`${nextStart.toFixed(2)}s (${Math.round(nextStart / FRAME)}f)`);
      if (timeline) {
        changeTimeline({
          ...timeline,
          payloads: timeline.payloads.map(p => p.id === d.id ? {...p, time: toSec(nextStart)} : p),
        }, 'timeline-drag:' + d.id);
      }
      return;
    }

    const span = Math.max(FRAME, d.initialEnd - d.initialStart);
    if (d.mode === 'move') {
      nextStart = clamp(snap(d.initialStart + deltaSec), 0, Math.max(0, d.duration - span));
      nextEnd = Math.min(d.duration, nextStart + span);
    } else if (d.mode === 'start') {
      nextStart = clamp(snap(d.initialStart + deltaSec), 0, Math.max(0, d.initialEnd - FRAME));
    } else {
      nextEnd = clamp(snap(d.initialEnd + deltaSec), Math.min(d.duration, d.initialStart + FRAME), d.duration);
    }
    setDragLabel(`${nextStart.toFixed(2)}–${nextEnd.toFixed(2)}s (${Math.round(nextStart / FRAME)}f–${Math.round(nextEnd / FRAME)}f)`);

    const group = 'timeline-drag:' + d.id;
    if (d.kind === 'clip' && timeline) {
      changeTimeline({
        ...timeline,
        clips: timeline.clips.map(c => c.id === d.id ? {...c, start: toSec(nextStart), end: toSec(nextEnd)} : c),
      }, group);
    } else if (d.kind === 'auto-cancel') {
      changeAutoCancel(nextStart, nextEnd, group);
    } else if (d.kind === 'edge-range') {
      changeEdgeRange(d.id, nextStart, nextEnd, group);
    }
  }

  function endDrag() {
    dragRef.current = null;
    setDragLabel(null);
  }

  const pct = (value: number) => `${clamp((value / duration) * 100, 0, 100)}%`;

  // Group named cancel channels on explicit timeline
  const cancelChannels = useMemo(() => {
    if (!timeline) return [];
    const list: string[] = [];
    for (const clip of timeline.clips) {
      if (clip.kind === 'cancel' && clip.channel && !list.includes(clip.channel)) list.push(clip.channel);
    }
    return list;
  }, [timeline]);

  const rulerTicks = useMemo(() => {
    const step = duration <= 0.6 ? 0.1 : duration <= 1.5 ? 0.2 : 0.5;
    const ticks: number[] = [];
    for (let t = 0; t <= duration + 0.0001; t += step) ticks.push(Number(t.toFixed(2)));
    if (ticks[ticks.length - 1] < duration - 0.02) ticks.push(Number(duration.toFixed(2)));
    return ticks;
  }, [duration]);

  const highlightedMode = selectedRule && skill && selectedRule.preSkills.includes(skill.id) ? (selectedRule.window?.mode ?? 'default') : null;
  const highlightedChannel = highlightedMode === 'channel' ? (selectedRule?.window?.channel ?? '') : null;

  return (
    <section className={`combo-timeline-dock ${collapsed ? 'is-collapsed' : ''}`}
      style={collapsed ? undefined : {height}}
      aria-label="底部交互时间轴">
      {!collapsed && (
        <div className="ctl-resize-handle"
          onPointerDown={event => {
            const startY = event.clientY;
            const startH = height;
            const move = (e: PointerEvent) => setHeight(clamp(startH + (startY - e.clientY), 130, 420));
            const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); };
            window.addEventListener('pointermove', move);
            window.addEventListener('pointerup', up);
          }}/>
      )}

      <header className="ctl-header">
        <button type="button" className="ctl-collapse-btn" onClick={() => setCollapsed(v => !v)}>
          {collapsed ? <ChevronUp size={13}/> : <ChevronDown size={13}/>}
          <strong>时间轴</strong>
        </button>

        {skill && meta ? (
          <>
            <span className="ctl-skill-badge" style={{'--skill-accent': meta.accent} as React.CSSProperties}>
              <i/>{skill.name}
            </span>
            <small className="ctl-meta-text">
              {meta.timelineKind === 'operation' ? '即时操作' : `${duration.toFixed(2)}s · ${Math.round(duration / FRAME)} 帧`}
              {selectedRule && selectedRule.preSkills.includes(skill.id) ? ' · 前置窗口联动' : ''}
            </small>
            {shared && <span className="ctl-shared-tag">共享轴 · 影响 {meta.sharedUserCount} 个技能</span>}
            {dragLabel && <span className="ctl-drag-readout">{dragLabel}</span>}
            {liveTime !== null && <span className="ctl-live-readout">当前 {liveTime.toFixed(2)}s</span>}
            <span className="grow"/>
            {!collapsed && timeline && (
              <div className="ctl-actions">
                <div className="popover-anchor">
                  <button type="button" onClick={() => setAddingChannel(v => !v)}><Plus size={11}/>取消窗</button>
                  {addingChannel && (
                    <div className="ctl-channel-menu">
                      <button type="button" onClick={() => addCancelClip(undefined)}>通用取消窗</button>
                      {CANCEL_CHANNELS.map(ch => (
                        <button key={ch.id} type="button" onClick={() => addCancelClip(ch.id)}>
                          <i style={{background: ch.color}}/>{ch.label}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
                <button type="button" className={showAdvanced ? 'is-active' : ''} onClick={() => setShowAdvanced(v => !v)}>
                  <SlidersHorizontal size={11}/> 详细
                </button>
              </div>
            )}
            {!collapsed && !timeline && meta.timelineKind === 'auto' && (
              <button type="button" onClick={upgradeToExplicitTimeline} title="升级为显式时间轴以添加命名取消轨与精细 Clip">
                <Sparkles size={11}/> 转为显式轴
              </button>
            )}
          </>
        ) : (
          <small className="ctl-meta-text">选择技能卡片或接续连线后，在此直接拖拽调整取消窗与动作节奏</small>
        )}
      </header>

      {!collapsed && skill && meta && (
        <div className="ctl-body">
          {meta.timelineKind === 'operation' ? (
            <p className="ctl-empty-msg">该技能为即时操作，不占用动作时间轴。</p>
          ) : (
            <div className="ctl-tracks-scroll">
              <div className="ctl-ruler-row">
                <div className="ctl-lane-label">标尺 (50Hz)</div>
                <div className="ctl-ruler-track">
                  {rulerTicks.map(tick => (
                    <span key={tick} className="ctl-tick" style={{left: pct(tick)}}>{tick.toFixed(2)}s</span>
                  ))}
                  {liveTime !== null && <b className="ctl-playhead" style={{left: pct(liveTime)}}/>}
                </div>
              </div>

              {/* Default Cancel Window Track */}
              <div className={`ctl-lane-row ${highlightedMode === 'default' ? 'is-highlighted' : ''}`}>
                <div className="ctl-lane-label">通用取消窗</div>
                <div className="ctl-lane-track">
                  {timeline ? (
                    timeline.clips.filter(c => c.kind === 'cancel' && !c.channel).map(clip => (
                      <div key={clip.id} className="ctl-bar is-cancel"
                        style={{left: pct(clip.start), width: pct(clip.end - clip.start)}}
                        title={`通用取消窗 ${clip.start.toFixed(2)}–${clip.end.toFixed(2)}s`}
                        onPointerDown={e => beginDrag(e, 'clip', 'move', clip.id, clip.start, clip.end)}
                        onPointerMove={moveDrag} onPointerUp={endDrag}>
                        <span className="ctl-handle left" onPointerDown={e => beginDrag(e, 'clip', 'start', clip.id, clip.start, clip.end)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                        <span className="ctl-bar-text">{clip.start.toFixed(2)}–{clip.end.toFixed(2)}</span>
                        <span className="ctl-handle right" onPointerDown={e => beginDrag(e, 'clip', 'end', clip.id, clip.start, clip.end)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                      </div>
                    ))
                  ) : castNode ? (() => {
                    const cStart = Number(castNode.parameters.cancelStart ?? 0);
                    const cEnd = Number(castNode.parameters.cancelEnd ?? 0);
                    if (cEnd <= cStart) {
                      return (
                        <button type="button" className="ctl-inline-add"
                          onClick={() => changeAutoCancel(toSec(duration * 0.3), toSec(duration * 0.9))}>
                          + 开启自动取消窗
                        </button>
                      );
                    }
                    return (
                      <div className="ctl-bar is-cancel"
                        style={{left: pct(cStart), width: pct(cEnd - cStart)}}
                        title={`自动取消窗 ${cStart.toFixed(2)}–${cEnd.toFixed(2)}s`}
                        onPointerDown={e => beginDrag(e, 'auto-cancel', 'move', 'auto', cStart, cEnd)}
                        onPointerMove={moveDrag} onPointerUp={endDrag}>
                        <span className="ctl-handle left" onPointerDown={e => beginDrag(e, 'auto-cancel', 'start', 'auto', cStart, cEnd)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                        <span className="ctl-bar-text">{cStart.toFixed(2)}–{cEnd.toFixed(2)}</span>
                        <span className="ctl-handle right" onPointerDown={e => beginDrag(e, 'auto-cancel', 'end', 'auto', cStart, cEnd)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                      </div>
                    );
                  })() : null}
                  {liveTime !== null && <b className="ctl-playhead" style={{left: pct(liveTime)}}/>}
                </div>
              </div>

              {/* Named Cancel Channel Tracks */}
              {timeline && cancelChannels.map(ch => (
                <div key={ch} className={`ctl-lane-row ${highlightedChannel === ch ? 'is-highlighted' : ''}`}>
                  <div className="ctl-lane-label" title={`命名取消轨：${ch}`}>
                    <i className="ctl-dot" style={{background: channelColor(ch)}}/>{ch}
                  </div>
                  <div className="ctl-lane-track">
                    {timeline.clips.filter(c => c.kind === 'cancel' && c.channel === ch).map(clip => (
                      <div key={clip.id} className="ctl-bar is-channel"
                        style={{left: pct(clip.start), width: pct(clip.end - clip.start), '--bar-color': channelColor(ch)} as React.CSSProperties}
                        title={`${ch} 取消窗 ${clip.start.toFixed(2)}–${clip.end.toFixed(2)}s`}
                        onPointerDown={e => beginDrag(e, 'clip', 'move', clip.id, clip.start, clip.end)}
                        onPointerMove={moveDrag} onPointerUp={endDrag}>
                        <span className="ctl-handle left" onPointerDown={e => beginDrag(e, 'clip', 'start', clip.id, clip.start, clip.end)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                        <span className="ctl-bar-text">{clip.start.toFixed(2)}–{clip.end.toFixed(2)}</span>
                        <span className="ctl-handle right" onPointerDown={e => beginDrag(e, 'clip', 'end', clip.id, clip.start, clip.end)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                      </div>
                    ))}
                    {liveTime !== null && <b className="ctl-playhead" style={{left: pct(liveTime)}}/>}
                  </div>
                </div>
              ))}

              {/* Edge-level Custom Range Overlays */}
              {customRangeEdges.map(rule => {
                const start = rule.window?.start ?? 0;
                const end = rule.window?.end ?? duration;
                const isSel = selectedRule?.id === rule.id;
                const targetName = project.skills.find(s => s.id === rule.toSkill)?.name ?? rule.toSkill;
                const color = inputColor(rule.input);
                return (
                  <div key={rule.id} className={`ctl-lane-row ${isSel ? 'is-highlighted' : ''}`}
                    onClick={() => onSelectRule(rule.id)}>
                    <div className="ctl-lane-label" title={`边级自定义时段：${rule.input} → ${targetName}`}>
                      <i className="ctl-dot" style={{background: color}}/>→ {targetName}
                    </div>
                    <div className="ctl-lane-track">
                      <div className="ctl-bar is-edge-range"
                        style={{left: pct(start), width: pct(Math.max(FRAME, end - start)), '--bar-color': color} as React.CSSProperties}
                        title={`${inputShortLabel(project, rule.input)} → ${targetName} (${start.toFixed(2)}–${end.toFixed(2)}s)`}
                        onPointerDown={e => beginDrag(e, 'edge-range', 'move', rule.id, start, end)}
                        onPointerMove={moveDrag} onPointerUp={endDrag}>
                        <span className="ctl-handle left" onPointerDown={e => beginDrag(e, 'edge-range', 'start', rule.id, start, end)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                        <span className="ctl-bar-text">{start.toFixed(2)}–{end.toFixed(2)}</span>
                        <span className="ctl-handle right" onPointerDown={e => beginDrag(e, 'edge-range', 'end', rule.id, start, end)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                      </div>
                      {liveTime !== null && <b className="ctl-playhead" style={{left: pct(liveTime)}}/>}
                    </div>
                  </div>
                );
              })}

              {/* Hitbox / Move / IFrame / Payload Tracks for explicit timeline */}
              {timeline && (['hitbox', 'move', 'iframe'] as const).map(kind => {
                const items = timeline.clips.filter(c => c.kind === kind);
                if (!items.length) return null;
                const label = kind === 'hitbox' ? '攻击判定' : kind === 'move' ? '动作位移' : '无敌帧';
                return (
                  <div key={kind} className="ctl-lane-row">
                    <div className="ctl-lane-label">{label}</div>
                    <div className="ctl-lane-track">
                      {items.map(clip => (
                        <div key={clip.id} className={`ctl-bar is-${kind}`}
                          style={{left: pct(clip.start), width: pct(clip.end - clip.start)}}
                          title={`${label} ${clip.start.toFixed(2)}–${clip.end.toFixed(2)}s`}
                          onPointerDown={e => beginDrag(e, 'clip', 'move', clip.id, clip.start, clip.end)}
                          onPointerMove={moveDrag} onPointerUp={endDrag}>
                          <span className="ctl-handle left" onPointerDown={e => beginDrag(e, 'clip', 'start', clip.id, clip.start, clip.end)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                          <span className="ctl-bar-text">{clip.start.toFixed(2)}–{clip.end.toFixed(2)}</span>
                          <span className="ctl-handle right" onPointerDown={e => beginDrag(e, 'clip', 'end', clip.id, clip.start, clip.end)} onPointerMove={moveDrag} onPointerUp={endDrag}/>
                        </div>
                      ))}
                      {liveTime !== null && <b className="ctl-playhead" style={{left: pct(liveTime)}}/>}
                    </div>
                  </div>
                );
              })}

              {timeline && !!timeline.payloads.length && (
                <div className="ctl-lane-row">
                  <div className="ctl-lane-label">效果触发</div>
                  <div className="ctl-lane-track">
                    {timeline.payloads.map(payload => (
                      <div key={payload.id} className="ctl-payload-diamond"
                        style={{left: pct(payload.time)}}
                        title={`效果触发 ${payload.time.toFixed(2)}s`}
                        onPointerDown={e => beginDrag(e, 'payload', 'move', payload.id, payload.time, payload.time)}
                        onPointerMove={moveDrag} onPointerUp={endDrag}/>
                    ))}
                    {liveTime !== null && <b className="ctl-playhead" style={{left: pct(liveTime)}}/>}
                  </div>
                </div>
              )}

              {showAdvanced && owner?.timeline && (
                <div className="ctl-advanced">
                  <TimelineProperties skill={owner} change={val => changeTimeline(val)} defaultCollapsed={false}/>
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </section>
  );
}
