import type {Project} from '../model';

export function defaultScene():Project['preview']{
  const positions=[[1,-2],[2,1],[5,3],[-.3,-4],[-3,3],[2,7],[6,0],[0,-2]];
  return {seed:1,energy:100,recovery:10,ammo:60,caster:{id:'caster',x:-9,z:0,hp:1000,atk:80,def:0,collisionRadius:.4},
    targets:positions.map(([x,z],i)=>({id:'target-'+(i+1),x,z,hp:1000,atk:0,def:0,collisionRadius:.4})),
    obstacles:[{id:'obstacle-1',x:-2,z:4,width:.8,depth:2.8}]};
}

export function restoreDefaultFormation(preview:Project['preview']):Project['preview']{
  const defaults=defaultScene(),used=new Set([preview.caster.id,...preview.targets.map(t=>t.id)]);
  const targets=defaults.targets.map((position,index)=>{
    const existing=preview.targets[index],template=existing??preview.targets[0]??position;
    let id=existing?.id??position.id,suffix=1;
    if(!existing){while(used.has(id))id=position.id+'-'+suffix++;used.add(id);}
    return {...template,id,x:position.x,z:position.z};
  });
  // Formation is presentation setup, not a reset of resource or actor stats.
  return {...preview,caster:{...preview.caster,x:defaults.caster.x,z:defaults.caster.z},targets,obstacles:defaults.obstacles};
}
