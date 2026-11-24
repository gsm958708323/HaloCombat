import type {RunEvent} from '../model';

export const runtimeEventFilters:[string,string][]=[['key','关键事件'],['all','全部事件'],['damage','伤害'],['cast','施放'],['spawn','生成'],['behavior','弹体与判定'],['lifecycle','生命周期'],['rejected','拒绝'],['execute','节点执行'],['placement','布置']];
const cast=new Set(['cast','operation','completed','cancelled','released','input','combo-resolved','combo-unmatched','combo-confirmation']);
const quiet=new Set(['execute','input','cue','cue-start','cue-stop','area-stay','area-pulse']);
export function matchesRuntimeEvent(kind:string,filter:string):boolean {
  if(filter==='all')return true;if(filter==='key')return !quiet.has(kind);
  if(filter==='damage')return kind==='damage'||kind==='critical';if(filter==='cast')return cast.has(kind)||kind.startsWith('skill-')&&!kind.endsWith('rejected');
  if(filter==='spawn')return kind==='spawn'||kind==='summon-spawned'||kind==='construct-spawned';
  if(filter==='behavior')return kind.startsWith('projectile-')||kind.startsWith('action-')||kind==='ray'||kind==='collision';
  if(filter==='lifecycle')return ['buff-','area-','summon-','construct-'].some(prefix=>kind.startsWith(prefix))||kind==='cleanup';
  if(filter==='rejected')return kind.endsWith('rejected')||kind==='projectile-teleport-blocked';return kind===filter;
}
export function runtimeEventView(events:RunEvent[],filter:string,since=0,limit=500) {
  const retained=events.filter(e=>e.sequence>since),matching=retained.filter(e=>matchesRuntimeEvent(e.kind,filter));
  const count=Math.max(0,Math.floor(limit));const rows=(count>0?matching.slice(-count):[]).sort((a,b)=>b.step-a.step||a.sequence-b.sequence);
  return {rows,retained,total:matching.length,hidden:Math.max(0,matching.length-rows.length)};
}
