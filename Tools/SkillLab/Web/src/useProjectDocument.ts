import { useCallback, useEffect, useRef, useState } from 'react';
import type { Project } from './model';
import { EditorHistory } from './authoring/EditorHistory';
import { ProjectSave, readDiskProject } from './authoring/projectIO';

type Update = Project | ((project:Project)=>Project);
export function useProjectDocument(report:(text:string)=>void) {
  const history=useRef<EditorHistory|null>(null);
  const persistence=useRef<ProjectSave|null>(null);
  const current=useRef<Project|null>(null);
  const group=useRef('');const groupNumber=useRef(0);
  const loadGeneration=useRef(0);
  const reportRef=useRef(report);reportRef.current=report;
  const [revision,changed]=useState(0);
  const refresh=useCallback(()=>{current.current=history.current?.current??null;changed(n=>n+1);},[]);
  const load=useCallback(async()=>{
    if(persistence.current?.saving)throw new Error('请等待当前保存完成');
    const generation=++loadGeneration.current;const before=history.current?.current;
    const result=await readDiskProject();
    if(generation!==loadGeneration.current)return;
    if(history.current?.current!==before)throw new Error('载入期间工程已更改，请重新载入磁盘');
    if(history.current)history.current.edit(result.project);
    else history.current=new EditorHistory(result.project);
    persistence.current=new ProjectSave(result.project,result.etag);refresh();
    reportRef.current('已载入最近保存的磁盘工程');
  },[refresh]);
  useEffect(()=>{void load().catch(e=>reportRef.current(String(e)));},[load]);
  const edit=useCallback((update:Update,remember=true,explicitGroup?:string)=>{
    const store=history.current;if(!store)return;
    const next=typeof update==='function'?update(store.current):update;
    if(store.edit(next,explicitGroup??group.current,remember))refresh();
  },[refresh]);
  const endGroup=useCallback(()=>{group.current='';history.current?.endGroup();},[]);
  const beginGroup=useCallback(()=>{group.current='field:'+ ++groupNumber.current;history.current?.endGroup();},[]);
  const undo=useCallback(()=>{endGroup();if(history.current?.undo())refresh();},[endGroup,refresh]);
  const redo=useCallback(()=>{endGroup();if(history.current?.redo())refresh();},[endGroup,refresh]);
  const save=useCallback(async()=>{
    const store=persistence.current;const value=current.current;if(!store||!value)return;
    try{
      const pending=store.save(value);refresh();await pending;
      reportRef.current(store.dirty(current.current!)?'请求时的工程已保存，之后的修改仍未保存':'工程已保存到磁盘');
    }catch(error){reportRef.current(String(error));}finally{refresh();}
  },[refresh]);
  const project=current.current;
  const dirty=!!project&&!!persistence.current?.dirty(project);
  useEffect(()=>{
    if(!dirty)return;
    const leaving=(event:BeforeUnloadEvent)=>{event.preventDefault();event.returnValue='';};
    window.addEventListener('beforeunload',leaving);return ()=>window.removeEventListener('beforeunload',leaving);
  },[dirty]);
  return {project,current,revision,edit,save,load,undo,redo,beginGroup,endGroup,dirty,
    canUndo:history.current?.canUndo??false,canRedo:history.current?.canRedo??false,
    saving:persistence.current?.saving??false,
    saveStatus:persistence.current?.saving?'保存中…':persistence.current?.failure?'保存失败':dirty?'未保存':'已保存到磁盘'};
}
