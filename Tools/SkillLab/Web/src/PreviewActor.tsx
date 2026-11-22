import {memo,useCallback} from 'react';

function circlePath(radius:number) {
  return `M${radius},0A${radius},${radius} 0 1 0 ${-radius},0A${radius},${radius} 0 1 0 ${radius},0Z`;
}

// Only visible glyph state crosses this boundary. Horizontal movement is painted
// directly by useActorAnimation and does not require reconciling a glyph.
interface Props {
  id:string; sceneId?:string|null; name:string; kind:string; radius:number; innerRadius?:number|null;
  height:number; hp?:number|null; maxHp?:number|null; remaining?:string;
  dead:boolean; stopped:boolean; hurt:boolean; ally:boolean; selected:boolean; buffs?:string;
  bind:(id:string,element:SVGGElement|null)=>void;
}
export const PreviewActor=memo(function PreviewActor({id,sceneId,name,kind,radius,innerRadius,height,hp,maxHp,remaining,dead,stopped,hurt,ally,selected,buffs,bind}:Props) {
  const ref=useCallback((element:SVGGElement|null)=>bind(id,element),[bind,id]);
  if(kind==='aoe') return <g ref={ref} pointerEvents="none">
    {innerRadius&&innerRadius>0&&innerRadius<radius
      ?<path aria-label="环形区域" d={circlePath(radius)+circlePath(innerRadius)} fillRule="evenodd" fill="#817ba3" fillOpacity=".20" stroke="#b1a4db" strokeWidth=".05"/>
      :<circle r={radius} fill="#817ba3" fillOpacity=".15" stroke="#b1a4db" strokeWidth=".05"/>}
    <text y={-radius-.2} textAnchor="middle" fill="#c3b8df" fontSize=".25">{remaining}s</text></g>;
  const projectile=kind==='projectile', caster=kind==='caster';
  const fill=caster?'#3b746a':projectile?'#e8d7a1':kind==='summon'||ally?'#4c9568':kind==='construct'?'#b37b3c':'#95664b';
  return <g ref={ref} data-scene-id={sceneId??undefined} data-entity-id={id} aria-label={sceneId?`布置 ${name}`:undefined}
    style={{cursor:sceneId&&!dead?'grab':undefined}} opacity={dead?.4:1}>
    {projectile&&height>0&&<><ellipse rx={Math.max(.12,radius*1.4)} ry={Math.max(.05,radius*.6)} fill="#0008"/>
      <line x1="0" x2="0" y1="0" y2={-height*.5} stroke="#b5c9c5" strokeWidth=".025"/>
      <text x=".2" y={-height*.5} fill="#cfdfde" fontSize=".22">h {height.toFixed(2)}</text></>}
    <circle cy={projectile?-Math.max(0,height)*.5:0} r={Math.max(.08,radius)} strokeDasharray={stopped?'.1 .07':undefined}
      fill={fill} stroke={hurt?'#fff':caster?'#a5d5c7':'#e5c2a0'} strokeWidth=".05"/>
    {selected&&<circle r={Math.max(.35,radius)+.12} fill="none" stroke="#c8d7a3" strokeWidth=".04" strokeDasharray=".1 .05"/>}
    {!projectile&&<><text y=".12" textAnchor="middle" fill="#fff" fontSize=".3">{caster?'C':name.match(/\d+$/)?.[0]??name.slice(0,2)}</text>
      <rect x="-.5" y="-.8" width="1" height=".06" fill="#39454a"/><rect x="-.5" y="-.8" width={Math.max(0,(hp??0)/(maxHp??1))} height=".06" fill="#d3bf92"/>
      <text y=".85" textAnchor="middle" fill="#becad0" fontSize=".22">{hp?.toFixed(0)} / {maxHp?.toFixed(0)}</text>
      {buffs&&<text y="1.1" textAnchor="middle" fill="#b1d3a6" fontSize=".2">{buffs}</text>}</>}
  </g>;
});
