import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNodesInitialized, useReactFlow } from '@xyflow/react';
import type { Connection, Edge, EdgeChange, NodeChange, Viewport } from '@xyflow/react';
import { kinds, nodeOutputs } from './model';
import type { Project,ParameterValue } from './model';
import type { EditorNode } from './SkillNode';
import type { useProjectDocument } from './useProjectDocument';
import { definitionAllocator, graphOf, saveViewport,selectGraph, updateGraph } from './authoring/contentEdits';
import {extractProjectile} from './authoring/projectileEdits';
import {extractArea} from './authoring/areaEdits';
import type { EditableGraph, GraphKey } from './authoring/contentEdits';
import { connectGraph, copySelection, deleteSelection, pasteSelection } from './authoring/graphEdits';
import type { GraphClipboard } from './authoring/graphEdits';

export function useGraphAuthoring(document:ReturnType<typeof useProjectDocument>,key:GraphKey,report:(text:string)=>void) {
  const flow=useReactFlow<EditorNode>();const initialized=useNodesInitialized();
  const [selectedNodes,setSelectedNodes]=useState<string[]>([]);const [selectedEdges,setSelectedEdges]=useState<string[]>([]);
  const [zoom,setZoom]=useState(1);const clipboard=useRef<GraphClipboard|null>(null);
  const [measurements,setMeasurements]=useState<Record<string,{width:number;height:number}>>({});
  const restored=useRef('');const restoring=useRef(false);const pasteNumber=useRef(0);
  const reconnecting=useRef<string|undefined>(undefined);
  const project=document.project;const graph=project?graphOf(project,key):undefined;const keyString=(project?.id??'')+'/'+key.kind+':'+key.id;
  const savedViewport=project?.editor?.viewports?.find(v=>v.kind===key.kind&&v.id===key.id);
  const viewportKey=JSON.stringify(savedViewport);
  const flowRef=useRef(flow);flowRef.current=flow;
  const editGraph=useCallback((update:(g:EditableGraph)=>EditableGraph,group?:string)=>document.edit(p=>updateGraph(p,key,update),true,group),[document.edit,key.kind,key.id]);
  useEffect(()=>{setSelectedNodes([]);setSelectedEdges([]);},[keyString]);
  useEffect(()=>{clipboard.current=null;},[project?.id]);
  useEffect(()=>{
    if(!key.id||!initialized)return;
    const saved=document.current.current?.editor?.viewports?.find(v=>v.kind===key.kind&&v.id===key.id);
    const actual=flowRef.current.getViewport();
    if(restored.current===keyString&&(!saved||Math.abs(saved.x-actual.x)<.01&&Math.abs(saved.y-actual.y)<.01&&Math.abs(saved.zoom-actual.zoom)<.00001))return;
    restoring.current=true;let cancelled=false;
    const frame=requestAnimationFrame(()=>requestAnimationFrame(()=>{
      if(cancelled)return;
      const pending=saved?flowRef.current.setViewport(saved):flowRef.current.fitView({padding:.15});
      void pending.finally(()=>{if(!cancelled){restored.current=keyString;restoring.current=false;setZoom(flowRef.current.getZoom());}});
    }));
    return ()=>{cancelled=true;cancelAnimationFrame(frame);restoring.current=false;};
  },[initialized,keyString,viewportKey,document.current,key.kind,key.id]);
  const remove=useCallback((ids:string[],edges:string[]=[])=>{
    document.endGroup();document.edit(p=>deleteSelection(p,key,ids,edges));setSelectedNodes([]);setSelectedEdges([]);
  },[document.edit,document.endGroup,key.kind,key.id]);
  const parameter=useCallback((id:string,field:string,value:ParameterValue)=>editGraph(g=>{
    const nodes=g.nodes.map(n=>n.id===id?{...n,parameters:{...n.parameters,[field]:value}}:n);
    const ports=nodeOutputs(nodes.find(n=>n.id===id)!).map(([p])=>p);
    return {...g,nodes,edges:g.edges.filter(e=>e.source!==id||ports.includes(e.sourcePort))};
  }),[editGraph]);
  const changeKind=useCallback((id:string,kind:string)=>editGraph(g=>{
    const nodes=g.nodes.map(n=>n.id===id?{...n,kind,parameters:Object.fromEntries(kinds[kind].fields.map(f=>[f.key,f.initial]))}:n);
    const ports=nodeOutputs(nodes.find(n=>n.id===id)!).map(([p])=>p);
    return {...g,nodes,edges:g.edges.filter(e=>e.source!==id||ports.includes(e.sourcePort))};
  }),[editGraph]);
  const nodes=useMemo<EditorNode[]>(()=>graph?.nodes.map(n=>({id:n.id,type:'skill',position:n.position,measured:measurements[keyString+':'+n.id],selected:selectedNodes.includes(n.id),deletable:n.kind!=='buff-events'&&n.id!==graph.root,data:{...n,definitionRoot:n.id===graph.root,authoredTimeline:!!graph.timeline||!!graph.timelineRef,
    counts:Object.fromEntries(nodeOutputs(n).map(([port])=>[port,graph.edges.filter(e=>e.source===n.id&&e.sourcePort===port).length])),
    blueprints:project?.summonBlueprints,buffs:project?.buffs,skills:project?.skills,projectiles:project?.projectiles,areas:project?.areas,views:project?.runtime?.views,change:parameter,changeKind,remove:(id)=>remove([id]),
    openArea:(id:string)=>document.edit(p=>selectGraph(p,p.editor?.selectedSkill??p.skills[0]?.id??'','','','',id),false),
    extractArea:(id:string)=>{try{document.endGroup();document.edit(p=>extractArea(p,key,id).project);}catch(e){report(String(e));}},
    openProjectile:(id:string)=>document.edit(p=>selectGraph(p,p.editor?.selectedSkill??p.skills[0]?.id??'','','',id),false),
    extractProjectile:(id:string)=>{try{document.endGroup();document.edit(p=>extractProjectile(p,key,id).project);}catch(e){report(String(e));}},
    toggle:(id,enabled)=>editGraph(g=>({...g,nodes:g.nodes.map(v=>v.id===id?{...v,enabled}:v)}))}}))??[],[graph,project?.summonBlueprints,project?.buffs,project?.skills,project?.projectiles,project?.areas,project?.runtime?.views,selectedNodes,parameter,changeKind,remove,editGraph,measurements,keyString]);
  const edges=useMemo<Edge[]>(()=>graph?.edges.map(e=>({id:e.id,source:e.source,sourceHandle:e.sourcePort,target:e.target,targetHandle:e.targetPort,
    selected:selectedEdges.includes(e.id),type:'default',reconnectable:true,style:{stroke:selectedEdges.includes(e.id)?'#f4d890':e.sourcePort==='target'?'#85abbc':'#d1b66e',strokeWidth:selectedEdges.includes(e.id)?3:2},animated:false}))??[],[graph,selectedEdges]);
  function nodeChanges(changes:NodeChange<EditorNode>[]){
    const dimensions=changes.filter(c=>c.type==='dimensions');
    if(dimensions.length)setMeasurements(previous=>{
      let next=previous;
      for(const change of dimensions){const value=change.dimensions;if(!value)continue;const id=keyString+':'+change.id,old=previous[id];
        if(!old||old.width!==value.width||old.height!==value.height){if(next===previous)next={...previous};next[id]=value;}}
      return next;
    });
    const selections=changes.filter(c=>c.type==='select');
    if(selections.length)setSelectedNodes(ids=>{const next=new Set(ids);for(const c of selections)if(c.type==='select'){if(c.selected)next.add(c.id);else next.delete(c.id);}return [...next];});
    const positions=changes.filter(c=>c.type==='position');
    if(positions.length){
      editGraph(g=>({...g,nodes:g.nodes.map(n=>{const position=positions.find(c=>c.id===n.id)?.position;return position?{...n,position}:n;})}),'drag:'+keyString);
      if(positions.some(c=>c.dragging===false))document.endGroup();
    }
    const removed=changes.filter(c=>c.type==='remove').map(c=>c.id);if(removed.length)remove(removed);
  }
  function edgeChanges(changes:EdgeChange[]){
    const selections=changes.filter(c=>c.type==='select');
    if(selections.length)setSelectedEdges(ids=>{const next=new Set(ids);for(const c of selections)if(c.type==='select'){if(c.selected)next.add(c.id);else next.delete(c.id);}return [...next];});
    const removed=changes.filter(c=>c.type==='remove').map(c=>c.id);if(removed.length)remove([],removed);
  }
  function connect(connection:Connection,id?:string){
    try{document.endGroup();editGraph(g=>connectGraph(g,connection,id,!!id));}
    catch(error){report(String(error));}
  }
  function add(kind:string){
    const p=document.current.current;if(!p||!key.id)return;document.endGroup();
    const position=flow.screenToFlowPosition({x:window.innerWidth*.55,y:250});const runtimeId=kind==='projectile'||kind==='delivery'||kind==='construct'?definitionAllocator(p)():0;
    editGraph(g=>({...g,nodes:[...g.nodes,{id:crypto.randomUUID(),kind,runtimeId,enabled:true,position,parameters:Object.fromEntries(kinds[kind].fields.map(f=>[f.key,f.initial]))}]}));
  }
  function copy(){
    const p=document.current.current;const g=p&&graphOf(p,key);if(!g||!selectedNodes.length)return;
    clipboard.current=copySelection(g,selectedNodes);pasteNumber.current=0;report('已复制 '+clipboard.current.nodes.length+' 个节点及内部连线');
  }
  function paste(){
    const p=document.current.current;if(!p||!clipboard.current)return;
    try{
      const point=flow.screenToFlowPosition({x:window.innerWidth*.52,y:230});++pasteNumber.current;point.x+=pasteNumber.current*24;point.y+=pasteNumber.current*24;
      const result=pasteSelection(p,key,clipboard.current,point);document.endGroup();document.edit(result.project);setSelectedNodes(result.ids);setSelectedEdges([]);
      report('已粘贴 '+result.ids.length+' 个节点'+(result.skipped?'；跳过 '+result.skipped+' 个不适用的根节点':''));
    }catch(error){report(String(error));}
  }
  function layout(){
    if(!graph)return;document.endGroup();
    const levels=new Map<string,number>();const depth=(id:string,visiting=new Set<string>()):number=>{
      if(levels.has(id))return levels.get(id)!;if(visiting.has(id))return 0;const next=new Set(visiting);next.add(id);
      const value=Math.max(0,...graph!.edges.filter(e=>e.target===id).map(e=>depth(e.source,next)+1));levels.set(id,value);return value;
    };
    const rows=new Map<number,number>();
    editGraph(g=>({...g,nodes:g.nodes.map(n=>{const level=depth(n.id),y=rows.get(level)??50;rows.set(level,y+(flow.getNode(n.id)?.measured?.height??280)+60);return {...n,position:{x:level*300,y}};})}));
    requestAnimationFrame(()=>void flow.fitView({padding:.15}));
  }
  function keyDown(event:React.KeyboardEvent){
    if((event.target as HTMLElement).closest('input,textarea,select,[contenteditable=true]'))return;
    const command=event.ctrlKey||event.metaKey;const key=event.key.toLowerCase();
    if(command&&key==='a'){event.preventDefault();setSelectedNodes(nodes.map(n=>n.id));setSelectedEdges(edges.map(e=>e.id));}
    else if(command&&key==='c'){event.preventDefault();copy();}
    else if(command&&key==='v'){event.preventDefault();paste();}
    else if(command&&key==='d'){event.preventDefault();copy();paste();}
    else if(key==='delete'||key==='backspace'){event.preventDefault();remove(selectedNodes,selectedEdges);}
  }
  function moved(viewport:Viewport){
    setZoom(viewport.zoom);
    if(!restoring.current&&restored.current===keyString&&key.id)document.edit(p=>saveViewport(p,key,viewport),false);
  }
  return {nodes,edges,graph,selectedNodes,selectedEdges,zoom,flow,editGraph,nodeChanges,edgeChanges,connect,add,copy,paste,remove,layout,keyDown,moved,
    moving:(viewport:Viewport)=>setZoom(viewport.zoom),canPaste:!!clipboard.current,
    reconnectStart:(id:string)=>{reconnecting.current=id;},reconnectEnd:()=>{reconnecting.current=undefined;},
    validConnection:(link:Connection|Edge)=>{try{if(!graph)return false;connectGraph(graph,link,reconnecting.current??'validation',!!reconnecting.current);return true;}catch{return false;}}};
}
