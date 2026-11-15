import { useCallback, useEffect, useRef, useState } from 'react';
import { api } from './model';
import type { Project, PreviewState } from './model';
import {previewSemanticKey,previewFrameBudget} from './authoring/previewTiming';
import type {RecordingStatus,ReplayComparison,ReplayResponse} from './replayModel';

export function usePreview(project: Project | null, selected: string, report: (message: string) => void,adoptReplay:(project:Project,root:string)=>void) {
  const [state, setState] = useState<PreviewState | null>(null);
  const [playing, setPlaying] = useState(false);
  const [speed, setSpeed] = useState(1);
  const [continuous, setContinuous] = useState(false);
  const [appliedKey, setAppliedKey] = useState('');
  const [error, setError] = useState('');
  const [recording,setRecording]=useState<RecordingStatus|null>(null),[comparison,setComparison]=useState<ReplayComparison|null>(null),[replaying,setReplaying]=useState(false);
  const replacing=useRef(false),adoptRef=useRef(adoptReplay);adoptRef.current=adoptReplay;
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
  const semanticKey=previewSemanticKey(project,selected);
  const semanticRef = useRef(semanticKey); semanticRef.current = semanticKey;
  const sessionKey = useRef('');
  const ready = appliedKey === semanticKey && !!session.current&&!replaying;
  const run = useCallback((command: unknown) => {
    const id = session.current; const generation = version.current;
    if (!id || sessionKey.current !== semanticRef.current||replacing.current) return Promise.resolve();
    queue.current = queue.current.catch(() => {}).then(async () => {
      if (generation !== version.current || sessionKey.current !== semanticRef.current) return;
      const result = await api<{ state: PreviewState;recording:RecordingStatus }>('/preview/' + id + '/command', command);
      if (generation === version.current) { latestState.current = result.state; setState(result.state);setRecording(result.recording);setComparison(null); }
    }).catch(error => reportRef.current(String(error)));
    return queue.current;
  }, []);
  const reset = useCallback(async () => {
    const document = projectRef.current;
    if (!document || replacing.current) return;
    const key = semanticRef.current;
    const generation = ++version.current;
    const old = session.current; session.current = '';
    setAppliedKey(''); setError(''); sessionKey.current = '';
    setRecording(null);setComparison(null);
    setPlaying(false); setContinuous(false); setState(null); latestState.current = null; accumulator.current = 0; held.current = false;
    if(old)void api('/preview/'+old,undefined,'DELETE').catch(()=>{});
    if(!document.skills.length){setError('工程中暂无技能，请先新建技能');return;}
    try {
      const result = await api<{ id: string; state: PreviewState;recording:RecordingStatus }>('/preview', document);
      if (generation !== version.current || key !== semanticRef.current) { await api('/preview/' + result.id, undefined, 'DELETE'); return; }
      session.current = result.id; sessionKey.current = key; setAppliedKey(key); setState(result.state); latestState.current = result.state;
      setRecording(result.recording);
      reportRef.current('真实 C# 预览已就绪');
    } catch (error) { if(generation===version.current){setError(String(error));reportRef.current(String(error));} }
  }, []);
  useEffect(() => {
    if(replaying)return;
    if(session.current&&sessionKey.current===semanticKey)return;
    ++version.current;
    const old = session.current; session.current = ''; sessionKey.current = '';
    setAppliedKey(''); setError(''); setPlaying(false); setContinuous(false); setState(null); latestState.current = null;
    setRecording(null);setComparison(null);
    if (old) void api('/preview/' + old, undefined, 'DELETE').catch(() => {});
    const timer = setTimeout(() => void reset(), 600);
    return () => clearTimeout(timer);
  }, [reset, semanticKey,replaying]);
  useEffect(() => () => { ++version.current; const id = session.current; if (id) void api('/preview/' + id, undefined, 'DELETE').catch(() => {}); }, []);
  const release = useCallback(() => { held.current = false; return run({ kind: 'release', ...aim.current }); }, [run]);
  const cancel = useCallback(() => { held.current = false; return run({ kind: 'cancel' }); }, [run]);
  useEffect(() => {
    const stop = () => { setPlaying(false);setContinuous(false);accumulator.current=0; void cancel(); void run({ kind: 'move', x: 0, z: 0 }); };
    const visibility=()=>{if(document.hidden)stop();};
    window.addEventListener('blur', stop);
    document.addEventListener('visibilitychange',visibility);
    return () => {window.removeEventListener('blur',stop);document.removeEventListener('visibilitychange',visibility);};
  }, [cancel,run]);
  useEffect(() => { if (!continuous && !held.current) void release(); }, [continuous,release]);
  useEffect(() => {
    lastTime.current = performance.now(); accumulator.current = 0;
    if (!playing || !ready) return;
    const timer = setInterval(async () => {
      const now = performance.now(); const delta = (now-lastTime.current)/1000; lastTime.current = now;
      if (busy.current) return;
      const budget=previewFrameBudget(accumulator.current,delta,speed);const steps=budget.steps;
      accumulator.current=budget.remainder;
      if (steps < 1) return;
      busy.current = true;
      try {
        if (continuous && !latestState.current?.channeling && !latestState.current?.charging) await run({ kind: 'cast', skillId: skill.current, ...aim.current });
        if (latestState.current?.channeling || latestState.current?.charging) await run({ kind: 'aim', ...aim.current });
        await run({ kind: 'step', steps });
      } finally { busy.current = false; }
    }, 60);
    return () => clearInterval(timer);
  }, [playing,ready,speed,continuous,run]);
  async function replayFile(text?:string){
    if(replacing.current)return;replacing.current=true;setReplaying(true);setComparison(null);setPlaying(false);setContinuous(false);held.current=false;accumulator.current=0;
    const generation=version.current,keyBefore=semanticRef.current;
    try{
      await queue.current;const id=session.current;
      let file=text;
      if(file===undefined){if(!id)throw new Error('当前预览尚未就绪');const response=await fetch('/api/preview/'+id+'/replay?skillId='+encodeURIComponent(skill.current));if(!response.ok){const error=await response.json();throw new Error(error.error??'无法读取输入记录');}file=await response.text();}
      const result=await api<ReplayResponse>('/replays',{text:file});
      if(generation!==version.current||semanticRef.current!==keyBefore){await api('/preview/'+result.id,undefined,'DELETE');throw new Error('校验期间配置已更改，请重新复现');}
      const replayProject=JSON.parse(result.projectJson) as Project;
      ++version.current;const key=previewSemanticKey(replayProject,result.rootSkillId);
      adoptRef.current(replayProject,result.rootSkillId);skill.current=result.rootSkillId;session.current=result.id;sessionKey.current=key;setAppliedKey(key);
      setState(result.state);latestState.current=result.state;setRecording(result.recording);setComparison(result.comparison);setError('');
      if(id)void api('/preview/'+id,undefined,'DELETE').catch(()=>{});
      reportRef.current('输入复现一致 · '+result.state.step+' 步 · '+result.comparison.values+' 个值');
    }catch(error){reportRef.current(String(error));throw error;}finally{replacing.current=false;setReplaying(false);}
  }
  async function exportReplay(){
    if(replacing.current)return;
    replacing.current=true;setReplaying(true);setPlaying(false);setContinuous(false);held.current=false;accumulator.current=0;
    const generation=version.current,keyBefore=semanticRef.current;
    try{
      await queue.current;
      const id=session.current;if(!id||sessionKey.current!==semanticRef.current)throw new Error('当前预览尚未就绪');
      const response=await fetch('/api/preview/'+id+'/replay?skillId='+encodeURIComponent(skill.current));
      if(!response.ok){const error=await response.json();throw new Error(error.error??'无法导出输入记录');}
      const text=await response.text();if(generation!==version.current||semanticRef.current!==keyBefore)throw new Error('配置已更改，请应用后重新导出记录');
      const url=URL.createObjectURL(new Blob([text],{type:'application/json'})),anchor=document.createElement('a');
      anchor.href=url;anchor.download='skill-replay.json';anchor.click();URL.revokeObjectURL(url);reportRef.current('输入记录已导出，含当前JSON、种子、逐步输入与结果快照');
    }finally{replacing.current=false;setReplaying(false);}
  }
  return { state, sessionId:session.current,ready, error, playing, setPlaying, speed, setSpeed, continuous, setContinuous, reset, aim, skill, release, cancel,
    recording,comparison,replaying,replayFile,exportReplay,
    cast: () => { held.current = true; return run({ kind: 'cast', skillId: skill.current, ...aim.current }); },
    input:(input:string)=>run({kind:'input',input,...aim.current}),
    move: (x:number,z:number) => run({ kind:'move',x,z }), interrupt: () => run({ kind:'interrupt' }),
    remove: (entityId:string)=>run({kind:'remove',entityId}),
    removeBuff:(actorId:string,instanceId:number)=>run({kind:'remove-buff',actorId,instanceId}),
    step: async () => { if (latestState.current?.channeling || latestState.current?.charging) await run({ kind:'aim',...aim.current }); await run({ kind: 'step', steps: 1 }); } };
}
