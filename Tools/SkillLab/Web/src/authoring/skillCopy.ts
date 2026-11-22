import { clone } from '../model';
import type { GraphNode, Project, RuntimeContent } from '../model';
import { withComboRuntime } from './comboEdits';
import { allGraphs, buffReferenceField, selectGraph } from './contentEdits';
import type { ImportCandidate } from './importCandidate';
import { mergeWithIdentities } from './mergeProjects';

const graphKeys=['skills','buffs','timelines','projectiles','areas'] as const;
const contentKeys=[...graphKeys,'summonBlueprints'] as const;
type DependencyKind=typeof contentKeys[number]|'views'|'cues';
export interface CopyDependency {
  kind:DependencyKind;
  name:string;
  from:string;
  to:string;
  resolution:'add'|'rename'|'reuse';
}

type Cue=RuntimeContent['cues'][number];
function sameCue(a:Cue,b:Cue) {
  return a.key===b.key&&a.kind===b.kind&&(a.resource??'')===(b.resource??'')
    &&(a.sound??'')===(b.sound??'')&&a.lifetime===b.lifetime;
}

function cueField(node:GraphNode) {
  if(node.kind==='cue')return 'id';
  if(node.kind==='projectile'||node.kind==='delivery')return 'cueId';
}

function referencedCue(node:GraphNode,field:string) {
  return node.parameters[field]??(node.kind==='cue'?1:0);
}

function referencedView(node:GraphNode) {
  const id=node.parameters.view;
  if(typeof id==='string'&&id&&!(node.kind==='construct'&&['barrel','crate'].includes(id)))return id;
}

function remapTrackingKeys(destination:Project,source:Project) {
  function nodesByKey(project:Project) {
    const nodes=new Map<string,GraphNode[]>();
    for(const {graph} of allGraphs(project))for(const node of graph.nodes){
      const key=node.parameters.trackingKey;
      if(typeof key!=='string'||!key)continue;
      const group=nodes.get(key)??[];
      group.push(node);
      nodes.set(key,group);
    }
    return nodes;
  }
  const target=nodesByKey(destination),incoming=nodesByKey(source);
  const used=new Set([...target.keys(),...incoming.keys()]);
  const renames:{from:string;to:string}[]=[];
  for(const [from,nodes] of incoming){
    if(!target.has(from))continue;
    let to=from+'-copy';
    let suffix=2;
    while(used.has(to))to=from+'-copy-'+suffix++;
    used.add(to);
    for(const node of nodes)node.parameters.trackingKey=to;
    renames.push({from,to});
  }
  return renames;
}

// Reserve both catalogs up front so resolving a collision leaves free source IDs intact.
function planCues(current:Cue[],incoming:Cue[]) {
  const used=new Set([...current,...incoming].map(c=>c.id));
  let next=1;
  const ids=new Map<number,number>();
  const definitions:Cue[]=[];
  const preview:CopyDependency[]=[];
  for(const cue of incoming){
    let id=cue.id;
    const conflict=current.find(c=>c.id===cue.id);
    const reuse=!!conflict&&sameCue(conflict,cue);
    if(conflict&&!reuse){
      while(used.has(next))next++;
      if(next>9999)throw new Error('没有可用的 Cue ID');
      id=next;used.add(id);
    }
    ids.set(cue.id,id);
    if(!reuse)definitions.push({...cue,id});
    preview.push({kind:'cues',name:cue.key,from:String(cue.id),to:String(id),resolution:reuse?'reuse':id===cue.id?'add':'rename'});
  }
  return {ids,definitions,preview};
}

export function planSkillCopy(destination:Project,candidate:ImportCandidate,rootId:string,newId?:()=>string) {
  if(!candidate.project.skills.some(skill=>skill.id===rootId))throw new Error('来源技能已不存在');
  const source=clone(candidate.project);
  const trackingKeys=remapTrackingKeys(destination,source);
  // The host supplies a dependency closure. Keep graph viewports if present, but exclude
  // project-level bindings even if a closure response also contains those fields.
  const included=new Map<string,Set<string>>(graphKeys.map(key=>[key,new Set((source[key]??[]).map(graph=>graph.id))]));
  const viewportKeys={skill:'skills',buff:'buffs',timeline:'timelines',projectile:'projectiles',area:'areas'};
  source.editor={viewports:source.editor?.viewports?.filter(view=>included.get(viewportKeys[view.kind])?.has(view.id))};

  const detachedReferences=[...candidate.detachedReferences];
  const sourceCueIds=new Set<number>(),sourceViewIds=new Set<string>();
  for(const {graph} of allGraphs(source))for(const node of graph.nodes){
    const field=cueField(node);
    const cue=field?referencedCue(node,field):undefined;
    if(typeof cue==='number'&&cue>0)sourceCueIds.add(cue);
    const view=referencedView(node);
    if(view)sourceViewIds.add(view);
  }
  const incomingRuntime=withComboRuntime(source).runtime!;
  const incomingCues=incomingRuntime.cues.filter(cue=>sourceCueIds.has(cue.id));
  const incomingViews=incomingRuntime.views?.filter(view=>view.kind==='body'&&sourceViewIds.has(view.id));
  const targetRuntime=withComboRuntime(destination).runtime!;
  const useRuntime=!!(destination.runtime||source.runtime&&(incomingCues.length||incomingViews?.length));
  const motorDiffers=Object.keys(targetRuntime.motor)
    .some(key=>targetRuntime.motor[key as keyof RuntimeContent['motor']]!==incomingRuntime.motor[key as keyof RuntimeContent['motor']]);
  const cues=planCues(useRuntime?targetRuntime.cues:[],useRuntime?incomingCues:[]);
  const bodyViews=new Set((incomingViews??[]).map(view=>view.id));
  const detachedPaths=new Set(candidate.detachedReferences.map(reference=>reference.path));
  const blueprints=new Set((source.summonBlueprints??[]).map(blueprint=>blueprint.id));

  // Dormant references omitted from the closure must stay unbound even if the target
  // happens to contain definitions with their old IDs.
  for(const key of graphKeys)for(const [index,graph] of (source[key]??[]).entries())for(const node of graph.nodes){
    const graphKey=key==='skills'||key==='buffs'?index:graph.id;
    const nodePath=`$.${key}[${graphKey}].nodes[${node.id}].parameters.`;
    function detach(field:string,originalId:string) {
      const path=nodePath+field;
      if(!detachedPaths.has(path)){
        detachedPaths.add(path);
        detachedReferences.push({path,nodeId:node.id,field,originalId});
      }
      delete node.parameters[field];
    }
    for(const field of Object.keys(node.parameters))if(detachedPaths.has(nodePath+field))delete node.parameters[field];
    function detachMissing(field:string,ids:Set<string>) {
      const id=node.parameters[field];
      if(typeof id==='string'&&id&&!ids.has(id))detach(field,id);
    }
    const buffField=buffReferenceField(node);
    if(buffField)detachMissing(buffField,included.get('buffs')!);
    if(node.kind==='cast'&&!node.parameters.requiredBuff)delete node.parameters.buffStackCost;
    if(node.kind==='projectile-ref')detachMissing('projectile',included.get('projectiles')!);
    if(node.kind==='area-ref')detachMissing('area',included.get('areas')!);
    if(node.kind==='delivery')detachMissing('blueprint',blueprints);
    if(node.kind==='cast')for(const field of ['fallbackSkill','recastSkill'])detachMissing(field,included.get('skills')!);
    if(referencedView(node))detachMissing('view',bodyViews);
    const field=cueField(node);
    if((useRuntime||source.runtime)&&field){
      const id=referencedCue(node,field);
      if(typeof id==='number'&&id>0){
        const mapped=cues.ids.get(id);
        if(mapped!==undefined){
          if(mapped!==id)node.parameters[field]=mapped;
        }else{
          detach(field,String(id));
          // Cue nodes require a positive ID. Disable a detached node so the compiler's
          // default ID cannot accidentally play a new workspace's Cue.
          if(node.kind==='cue')node.enabled=false;
        }
      }
    }
  }

  source.runtime=useRuntime?{
    motor:targetRuntime.motor,actors:[],combos:[],cues:cues.definitions,views:incomingViews
  }:undefined;
  const mergeBase=useRuntime&&!destination.runtime?{...destination,runtime:targetRuntime}:destination;
  const {project,identities}=mergeWithIdentities(mergeBase,source,newId);
  const root=identities.skills.get(rootId)!;
  if(useRuntime&&project.runtime){
    const {cues,views}=project.runtime;
    project.runtime={...targetRuntime,cues,views};
  }
  const dependencies:CopyDependency[]=[];
  for(const kind of contentKeys){
    for(const item of source[kind]??[]){
      const to=identities[kind].get(item.id)!;
      dependencies.push({kind,name:item.name??item.id,from:item.id,to,resolution:to===item.id?'add':'rename'});
    }
  }
  for(const view of source.runtime?.views??[]){
    const to=identities.views.get(view.id)!;
    dependencies.push({kind:'views',name:view.resource,from:view.id,to,resolution:to===view.id?'add':'rename'});
  }
  dependencies.push(...cues.preview);
  return {project:selectGraph(project,root),root,motorDiffers,detachedReferences,dependencies,trackingKeys};
}
export type SkillCopyPlan=ReturnType<typeof planSkillCopy>;
