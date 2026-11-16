import type {Project} from '../model';

export function defaultScene():Project['preview']{
  const positions=[[1,-2],[2,1],[5,3],[-.3,-4],[-3,3],[2,7],[6,0],[0,-2]];
  return {seed:1,energy:100,recovery:10,ammo:60,caster:{id:'caster',x:-9,z:0,hp:1000,atk:80,def:0,collisionRadius:.4},
    targets:positions.map(([x,z],i)=>({id:'target-'+(i+1),x,z,hp:1000,atk:0,def:0,collisionRadius:.4})),
    obstacles:[{id:'obstacle-1',x:-2,z:4,width:.8,depth:2.8}]};
}
