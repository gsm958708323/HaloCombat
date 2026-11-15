import type { Project,SharedProjectile } from '../model';
import { definitionAllocator,selectGraph } from './contentEdits';
import type { GraphKey } from './contentEdits';
import { extractBody } from './sharedBodyEdits';

export const extractProjectile=(project:Project,key:GraphKey,nodeId:string,newId:()=>string=()=>crypto.randomUUID())=>extractBody(project,key,nodeId,'projectile',newId);
export function newProjectile(project:Project,skillId:string,newId:()=>string=()=>crypto.randomUUID()) {
  const id=newId(),root=newId();const value:SharedProjectile={id,name:'新共享弹体',root,nodes:[{id:root,kind:'projectile',runtimeId:definitionAllocator(project)(),position:{x:0,y:80},parameters:{speed:12,range:24,radius:.18,maxHits:1}}],edges:[]};
  return {id,project:selectGraph({...project,projectiles:[...(project.projectiles??[]),value]},skillId,'','',id)};
}
