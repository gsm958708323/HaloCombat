import type { Project,SharedArea } from '../model';
import { definitionAllocator,selectGraph } from './contentEdits';
import type { GraphKey } from './contentEdits';
import { extractBody } from './sharedBodyEdits';

export const extractArea=(project:Project,key:GraphKey,nodeId:string,newId:()=>string=()=>crypto.randomUUID())=>extractBody(project,key,nodeId,'area',newId);
export function newArea(project:Project,skillId:string,newId:()=>string=()=>crypto.randomUUID()) {
  const id=newId(),root=newId();const value:SharedArea={id,name:'新共享区域',root,
    nodes:[{id:root,kind:'delivery',runtimeId:definitionAllocator(project)(),position:{x:0,y:80},parameters:{mode:'area',persistent:true,radius:1.5,duration:2,interval:.5,pulseOnSpawn:true}}],edges:[]};
  return {id,project:selectGraph({...project,areas:[...(project.areas??[]),value]},skillId,'','','',id)};
}
