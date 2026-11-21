import { useCallback, useEffect, useRef, useState } from 'react';
import type { Project } from './model';
import { EditorHistory } from './authoring/EditorHistory';
import { ProjectSave, readDiskProject, rememberedWorkspace, rememberWorkspace } from './authoring/projectIO';
import type { Workspace } from './authoring/projectIO';

type Update = Project | ((project:Project)=>Project);
export function useProjectDocument(report:(text:string)=>void) {
  const history=useRef<EditorHistory|null>(null);
  const persistence=useRef<ProjectSave|null>(null);
  const current=useRef<Project|null>(null);
  const group=useRef('');const groupNumber=useRef(0);
  const loading=useRef(false);
  const reportRef=useRef(report);reportRef.current=report;
  const [revision,changed]=useState(0);
  const [publishFeedback,setPublishFeedback]=useState<{text:string;error:boolean}|null>(null);
  const refresh=useCallback(()=>{current.current=history.current?.current??null;changed(n=>n+1);},[]);
  const load=useCallback(async(workspace?:Workspace,mode:'save'|'discard'|'clean'='clean')=>{
    const store=persistence.current;
    const before=current.current;
    if(loading.current||store?.busy)throw new Error('请等待正在进行的工程操作完成');
    const requested=workspace??store?.workspace??rememberedWorkspace();
    const switching=!!store&&requested!==store.workspace;
    if(switching&&mode==='clean'&&before&&store.dirty(before))throw new Error('当前工程有未保存的修改');
    loading.current=true;
    refresh();
    try {
      if(switching&&mode==='save'&&before&&store.dirty(before))await store.save(before);
      const result=await readDiskProject(requested);
      if(history.current&&store?.workspace===result.workspace)history.current.edit(result.project);
      else history.current=new EditorHistory(result.project);
      group.current='';history.current.endGroup();
      persistence.current=new ProjectSave(result.project,result.etag,result.workspace);
      rememberWorkspace(result.workspace);
      setPublishFeedback(null);
      reportRef.current('已载入最近保存的磁盘工程');
    } finally {
      loading.current=false;
      refresh();
    }
  },[refresh]);
  useEffect(()=>{void load().catch(e=>reportRef.current(String(e)));},[load]);
  const workspace=persistence.current?.workspace;
  const edit=useCallback((update:Update,remember=true,explicitGroup?:string)=>{
    const store=history.current;
    if(!store||loading.current||persistence.current?.workspace!==workspace)return;
    const next=typeof update==='function'?update(store.current):update;
    if(store.edit(next,explicitGroup??group.current,remember))refresh();
  },[refresh,workspace]);
  const endGroup=useCallback(()=>{group.current='';history.current?.endGroup();},[]);
  const beginGroup=useCallback(()=>{group.current='field:'+ ++groupNumber.current;history.current?.endGroup();},[]);
  const undo=useCallback(()=>{if(loading.current)return;endGroup();if(history.current?.undo())refresh();},[endGroup,refresh]);
  const redo=useCallback(()=>{if(loading.current)return;endGroup();if(history.current?.redo())refresh();},[endGroup,refresh]);
  const save=useCallback(async()=>{
    const store=persistence.current;const value=current.current;
    if(!store||!value||store.busy||loading.current)return;
    try{
      const pending=store.save(value);refresh();await pending;
      reportRef.current(store.dirty(current.current!)?'请求时的工程已保存，之后的修改仍未保存':'工程已保存到磁盘');
    }catch(error){reportRef.current(String(error));}finally{refresh();}
  },[refresh]);
  const publishArena=useCallback(async()=>{
    const store=persistence.current;const value=current.current;
    if(!store||!value||store.workspace!=='arena'||store.busy||loading.current)return;
    setPublishFeedback(null);
    try {
      const pending=store.publishArena(value);refresh();
      const result=await pending;
      const extra=store.dirty(current.current!)?' 当前还有发布请求之后的未保存修改。':'';
      setPublishFeedback({text:result.message+extra,error:false});
    } catch(error){setPublishFeedback({text:String(error),error:true});} finally {refresh();}
  },[refresh]);
  const project=current.current;
  const dirty=!!project&&!!persistence.current?.dirty(project);
  useEffect(()=>{
    if(!dirty)return;
    const leaving=(event:BeforeUnloadEvent)=>{event.preventDefault();event.returnValue='';};
    window.addEventListener('beforeunload',leaving);return ()=>window.removeEventListener('beforeunload',leaving);
  },[dirty]);
  return {project,current,revision,edit,save,publishArena,load,undo,redo,beginGroup,endGroup,dirty,
    workspace,loading:loading.current,publishFeedback,dismissPublishFeedback:()=>setPublishFeedback(null),
    canUndo:!loading.current&&(history.current?.canUndo??false),canRedo:!loading.current&&(history.current?.canRedo??false),
    saving:persistence.current?.saving??false,publishing:persistence.current?.publishing??false,
    saveStatus:persistence.current?.saving?'保存中…':persistence.current?.publishing?'发布中…':loading.current?'载入中…':persistence.current?.failure?'保存失败':dirty?'未保存':'已保存到磁盘'};
}
