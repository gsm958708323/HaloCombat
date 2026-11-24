import type { VolumeView } from './model';

function path(v:VolumeView){
  const r=v.length,w=v.width/2;
  if(v.shape==='thrust')return `M0,${-w}H${r}V${w}H0Z`;
  if(v.shape==='capsule')return `M0,${-w}H${r}A${w},${w} 0 0 1 ${r},${w}H0A${w},${w} 0 0 1 0,${-w}Z`;
  if(v.shape==='circle'||v.angle>=360)return `M${r},0A${r},${r} 0 1 0 ${-r},0A${r},${r} 0 1 0 ${r},0Z`;
  const a=v.angle*Math.PI/360;const x=r*Math.cos(a),y=r*Math.sin(a);
  return `M0,0L${x},${-y}A${r},${r} 0 ${v.angle>180?1:0} 1 ${x},${y}Z`;
}
export function SpatialVolumes({volumes}:{volumes:VolumeView[]}){
  return <>{volumes.map(v=><g key={v.sequence} transform={`translate(${v.x},${-v.z}) rotate(${-v.yaw})`}>
    <path d={path(v)} fill="#d5b070" fillOpacity=".15" stroke="#f0d29a" strokeWidth=".05"/>
    {(v.shape==='fan'||v.shape==='circle')&&(()=>{
      const angle=Math.min(360,v.angle),progress=Math.max(0,Math.min(1,v.progress??0)),edge=(-angle/2+progress*angle)*Math.PI/180;
      const start=Math.max(-angle/2,edge*180/Math.PI-35)*Math.PI/180,r=v.length;
      return <g aria-label="横扫挥砍动画"><path className="sweep-trail" d={`M0,0L${r*Math.cos(start)},${r*Math.sin(start)}A${r},${r} 0 0 1 ${r*Math.cos(edge)},${r*Math.sin(edge)}Z`}/>
        <path className="sweep-edge" d={`M${r*.1*Math.cos(edge)},${r*.1*Math.sin(edge)}L${r*Math.cos(edge)},${r*Math.sin(edge)}`}/></g>;
    })()}
  </g>)}</>;
}
