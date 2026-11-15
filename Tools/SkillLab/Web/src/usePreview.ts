import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from './model';
import type { Project, PreviewState } from './model';

export function usePreview(project: Project | null, selected: string, report: (message: string) => void) {
  const [state, setState] = useState<PreviewState | null>(null);
  const [playing, setPlaying] = useState(false);
  const [speed, setSpeed] = useState(1);
  const [continuous, setContinuous] = useState(false);
  const [appliedKey, setAppliedKey] = useState('');
  const [error, setError] = useState('');
  const session = useRef('');
  const version = useRef(0);
  const queue = useRef(Promise.resolve());
  const skill = useRef('');
  const aim = useRef({ x: 0, z: 0 });
  const lastTime = useRef(performance.now());
  const accumulator = useRef(0);
  const busy = useRef(false);
  const latestState = useRef(state); latestState.current = state;
  const held = useRef(false);
  const reportRef = useRef(report); reportRef.current = report;
  const projectRef = useRef(project); projectRef.current = project;
  const semanticKey = JSON.stringify([selected, project && {
    ...project,
    editor: undefined,
    skills: project.skills.map(s => ({ ...s, nodes: s.nodes.map(n => ({ ...n, position: undefined })) })),
    buffs: project.buffs?.map(b => ({ ...b, nodes: b.nodes.map(n => ({ ...n, position: undefined })) })),
    timelines:project.timelines?.map(t=>({...t,nodes:t.nodes.map(n=>({...n,position:undefined}))})),
    areas:project.areas?.map(t=>({...t,nodes:t.nodes.map(n=>({...n,position:undefined}))})),
    projectiles:project.projectiles?.map(t=>({...t,nodes:t.nodes.map(n=>({...n,position:undefined}))})),
  }]);
  const semanticRef = useRef(semanticKey); semanticRef.current = semanticKey;
  const sessionKey = useRef('');
  const ready = appliedKey === semanticKey && !!session.current;
  const run = useCallback((command: unknown) => {
    const id = session.current; const generation = version.current;
    if (!id || sessionKey.current !== semanticRef.current) return Promise.resolve();
    queue.current = queue.current.catch(() => {}).then(async () => {
      if (generation !== version.current || sessionKey.current !== semanticRef.current) return;
      const result = await api<{ state: PreviewState }>('/preview/' + id + '/command', command);
      if (generation === version.current) { latestState.current = result.state; setState(result.state); }
    }).catch(error => reportRef.current(String(error)));
    return queue.current;
  }, []);
  const reset = useCallback(async () => {
    const document = projectRef.current;
    if (!document) return;
    const key = semanticRef.current;
    const generation = ++version.current;
    const old = session.current; session.current = '';
    setAppliedKey(''); setError(''); sessionKey.current = '';
    setPlaying(false); setContinuous(false); setState(null); latestState.current = null; accumulator.current = 0; held.current = false;
    if(old)void api('/preview/'+old,undefined,'DELETE').catch(()=>{});
    if(!document.skills.length){setError('工程中暂无技能，请先新建技能');return;}
    try {
      const result = await api<{ id: string; state: PreviewState }>('/preview', document);
      if (generation !== version.current || key !== semanticRef.current) { await api('/preview/' + result.id, undefined, 'DELETE'); return; }
      session.current = result.id; sessionKey.current = key; setAppliedKey(key); setState(result.state); latestState.current = result.state;
      reportRef.current('真实 C# 预览已就绪');
    } catch (error) { if(generation===version.current){setError(String(error));reportRef.current(String(error));} }
  }, []);
  useEffect(() => {
    ++version.current;
    const old = session.current; session.current = ''; sessionKey.current = '';
    setAppliedKey(''); setError(''); setPlaying(false); setContinuous(false); setState(null); latestState.current = null;
    if (old) void api('/preview/' + old, undefined, 'DELETE').catch(() => {});
    const timer = setTimeout(() => void reset(), 600);
    return () => clearTimeout(timer);
  }, [reset, semanticKey]);
  useEffect(() => () => { ++version.current; const id = session.current; if (id) void api('/preview/' + id, undefined, 'DELETE').catch(() => {}); }, []);
  const release = useCallback(() => { held.current = false; return run({ kind: 'release', ...aim.current }); }, [run]);
  const cancel = useCallback(() => { held.current = false; return run({ kind: 'cancel' }); }, [run]);
  useEffect(() => {
    const stop = () => { setContinuous(false); void cancel(); void run({ kind: 'move', x: 0, z: 0 }); };
    window.addEventListener('blur', stop);
    return () => window.removeEventListener('blur', stop);
  }, [cancel,run]);
  useEffect(() => { if (!continuous && !held.current) void release(); }, [continuous,release]);
  useEffect(() => {
    lastTime.current = performance.now(); accumulator.current = 0;
    if (!playing || !ready) return;
    const timer = setInterval(async () => {
      const now = performance.now(); const delta = Math.min(.2,(now-lastTime.current)/1000); lastTime.current = now;
      if (busy.current) return;
      accumulator.current += delta * speed;
      const steps = Math.min(20,Math.floor(accumulator.current/.02));
      if (steps < 1) return;
      accumulator.current -= steps * .02;
      busy.current = true;
      try {
        if (continuous && !latestState.current?.channeling && !latestState.current?.charging) await run({ kind: 'cast', skillId: skill.current, ...aim.current });
        if (latestState.current?.channeling || latestState.current?.charging) await run({ kind: 'aim', ...aim.current });
        await run({ kind: 'step', steps });
      } finally { busy.current = false; }
    }, 60);
    return () => clearInterval(timer);
  }, [playing,ready,speed,continuous,run]);
  return { state, ready, error, playing, setPlaying, speed, setSpeed, continuous, setContinuous, reset, aim, skill, release, cancel,
    cast: () => { held.current = true; return run({ kind: 'cast', skillId: skill.current, ...aim.current }); },
    input:(input:string)=>run({kind:'input',input,...aim.current}),
    move: (x:number,z:number) => run({ kind:'move',x,z }), interrupt: () => run({ kind:'interrupt' }),
    remove: (entityId:string)=>run({kind:'remove',entityId}),
    removeBuff:(actorId:string,instanceId:number)=>run({kind:'remove-buff',actorId,instanceId}),
    step: async () => { if (latestState.current?.channeling || latestState.current?.charging) await run({ kind:'aim',...aim.current }); await run({ kind: 'step', steps: 1 }); } };
}
