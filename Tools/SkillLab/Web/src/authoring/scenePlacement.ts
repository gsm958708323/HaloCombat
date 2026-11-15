import type {ActorInitial,ObstacleView,Project} from '../model';

export interface WorldPoint {x:number;z:number}
export interface SceneBounds {left:number;right:number;bottom:number;top:number}
export interface WorldViewport {x:number;y:number;width:number;height:number}

export function sceneActor(project:Project,id:string):ActorInitial|undefined {
  return project.preview.caster.id===id?project.preview.caster:project.preview.targets.find(a=>a.id===id);
}
export function scenePlacementFree(point:WorldPoint,radius:number,obstacles:ObstacleView[]):boolean {
  if(!Number.isFinite(point.x)||!Number.isFinite(point.z))return false;
  return !obstacles.some(b=>{
    const x=Math.max(b.x-b.width/2,Math.min(b.x+b.width/2,point.x)),z=Math.max(b.z-b.depth/2,Math.min(b.z+b.depth/2,point.z));
    return radius>0?(point.x-x)**2+(point.z-z)**2<radius**2-.000001:point.x>b.x-b.width/2&&point.x<b.x+b.width/2&&point.z>b.z-b.depth/2&&point.z<b.z+b.depth/2;
  });
}
export function moveSceneActor(project:Project,id:string,point:WorldPoint):Project {
  const actor=sceneActor(project,id);if(!actor)throw new Error('试验角色不存在');
  if(!scenePlacementFree(point,actor.collisionRadius??.4,project.preview.obstacles??[]))throw new Error('角色位置与障碍重叠');
  return {...project,preview:{...project.preview,caster:id===project.preview.caster.id?{...actor,...point}:project.preview.caster,
    targets:project.preview.targets.map(a=>a.id===id?{...a,...point}:a)}};
}
export function removeSceneTarget(project:Project,id:string):Project {
  if(id===project.preview.caster.id)throw new Error('施法者不能从试验场删除');
  return {...project,preview:{...project.preview,targets:project.preview.targets.filter(a=>a.id!==id)}};
}
function nextIdentity(prefix:string,used:string[]):string {let n=1;while(used.includes(prefix+n))n++;return prefix+n;}
function candidates(origin:WorldPoint):WorldPoint[] {
  const points:WorldPoint[]=[];
  for(let ring=0;ring<=24;ring++)for(let x=-ring;x<=ring;x++)for(let z=-ring;z<=ring;z++)
    if(Math.max(Math.abs(x),Math.abs(z))===ring)points.push({x:Number((origin.x+x*1.5).toFixed(3)),z:Number((origin.z+z*1.5).toFixed(3))});
  return points;
}
export function addSceneTarget(project:Project,origin:WorldPoint={x:0,z:0}):Project {
  if(project.preview.targets.length>=128)throw new Error('试验目标已达到128个上限');
  const actors=[project.preview.caster,...project.preview.targets],radius=.4;
  const point=candidates(origin).find(p=>scenePlacementFree(p,radius,project.preview.obstacles??[])&&actors.every(a=>(a.x-p.x)**2+(a.z-p.z)**2>(radius+(a.collisionRadius??.4)+.5)**2));
  if(!point)throw new Error('视图附近没有可用的目标位置');
  return {...project,preview:{...project.preview,targets:[...project.preview.targets,{id:nextIdentity('target-',actors.map(a=>a.id)),...point,hp:1000,atk:0,def:0}]}};
}
export function addSceneObstacle(project:Project,origin:WorldPoint={x:-2,z:0}):Project {
  const obstacles=project.preview.obstacles??[];if(obstacles.length>=128)throw new Error('试验障碍已达到128个上限');
  const actors=[project.preview.caster,...project.preview.targets];
  const point=candidates(origin).find(p=>{
    const box:ObstacleView={id:'candidate',...p,width:.5,depth:3};
    return actors.every(a=>scenePlacementFree(a,a.collisionRadius??.4,[box]))&&obstacles.every(b=>Math.abs(p.x-b.x)>(b.width+.5)/2+.2||Math.abs(p.z-b.z)>(b.depth+3)/2+.2);
  });
  if(!point)throw new Error('视图附近没有可用的障碍位置');
  return {...project,preview:{...project.preview,obstacles:[...obstacles,{id:nextIdentity('obstacle-',obstacles.map(b=>b.id)),...point,width:.5,depth:3}]}};
}
export function sceneBounds(project:Project):SceneBounds {
  const actors=[project.preview.caster,...project.preview.targets],boxes=project.preview.obstacles??[];
  return {left:Math.min(...actors.map(a=>a.x-1),...boxes.map(b=>b.x-b.width/2)),right:Math.max(...actors.map(a=>a.x+1),...boxes.map(b=>b.x+b.width/2)),
    bottom:Math.min(...actors.map(a=>a.z-1),...boxes.map(b=>b.z-b.depth/2)),top:Math.max(...actors.map(a=>a.z+1),...boxes.map(b=>b.z+b.depth/2))};
}
export function fitScene(bounds:SceneBounds,aspect:number):WorldViewport {
  const ratio=Number.isFinite(aspect)&&aspect>0?aspect:4/3;
  const width=Math.max(12,bounds.right-bounds.left+4,(bounds.top-bounds.bottom+4)*ratio),height=width/ratio;
  return {x:(bounds.left+bounds.right-width)/2,y:-(bounds.top+bounds.bottom+height)/2,width,height};
}
export function zoomScene(view:WorldViewport,point:WorldPoint,factor:number):WorldViewport {
  const width=Math.max(6,Math.min(160,view.width*factor)),scale=width/view.width;
  return {x:point.x-(point.x-view.x)*scale,y:-point.z-(-point.z-view.y)*scale,width,height:view.height*scale};
}
