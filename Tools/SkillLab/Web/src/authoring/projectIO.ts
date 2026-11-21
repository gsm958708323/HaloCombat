import { clone } from '../model';
import type { Project } from '../model';
import { prepareImportedLayout } from './importLayout';

export type Workspace = 'arena' | 'lab';
export const workspaceLabel:Record<Workspace,string> = {arena:'Arena · 正式工程',lab:'Lab · 实验工程'};
export const otherWorkspace = (workspace:Workspace):Workspace => workspace==='arena'?'lab':'arena';
const storageKey='halo-skill-lab.workspace';
const isWorkspace=(value:unknown):value is Workspace=>value==='arena'||value==='lab';

export function rememberedWorkspace():Workspace|undefined {
  try {
    const value=localStorage.getItem(storageKey);
    return isWorkspace(value)?value:undefined;
  } catch { return undefined; }
}
export function rememberWorkspace(workspace:Workspace) {
  try { localStorage.setItem(storageKey,workspace); } catch { /* Storage may be disabled. */ }
}

export interface DiskProject { project:Project;etag:string;workspace:Workspace }
const projectURL=(workspace?:Workspace)=>'/api/project'+(workspace?'?workspace='+workspace:'');
export async function readDiskProject(workspace?:Workspace):Promise<DiskProject> {
  const response=await fetch(projectURL(workspace),{cache:'no-store'});
  if(!response.ok)throw new Error('无法读取磁盘工程：'+response.status);
  const resolved=response.headers.get('X-Workspace');
  const etag=response.headers.get('ETag');
  if(!isWorkspace(resolved)||(workspace&&resolved!==workspace)||!etag)throw new Error('磁盘工程响应缺少正确的工作区或版本标记');
  const project:Project=await response.json();
  return {project:response.headers.get('X-Needs-Layout')==='true'?prepareImportedLayout(project):project,etag,workspace:resolved};
}
export async function writeDiskProject(project:Project,etag:string,workspace:Workspace):Promise<string> {
  const response=await fetch(projectURL(workspace),{method:'POST',headers:{'Content-Type':'application/json','If-Match':etag},body:JSON.stringify(project)});
  if(!response.ok){const data=await response.json().catch(()=>null);throw new Error(data?.error??data?.detail??'保存失败：'+response.status);}
  const updated=response.headers.get('ETag');
  if(!updated)throw new Error('保存响应缺少版本标记，请重新载入磁盘工程');
  return updated;
}
export async function publishDiskArena(etag:string):Promise<{message:string}> {
  const response=await fetch('/api/runtime/publish?workspace=arena',{method:'POST',headers:{'If-Match':etag}});
  const result=await response.json().catch(()=>null);
  if(!response.ok)throw new Error(result?.error??result?.detail??'发布失败：'+response.status);
  return result;
}

// Acknowledgement applies to the captured snapshot, never to edits made during the request.
export class ProjectSave {
  saving=false;
  publishing=false;
  failure='';
  private savedKey:string;
  private etag:string;
  constructor(project:Project,etag:string,readonly workspace:Workspace){this.savedKey=JSON.stringify(project);this.etag=etag;}
  dirty(project:Project){return this.savedKey!==JSON.stringify(project);}
  get busy(){return this.saving||this.publishing;}
  async save(project:Project,write=writeDiskProject){
    if(this.busy)throw new Error(this.saving?'正在保存，请等待当前写入完成':'正在发布，请等待当前操作完成');
    await this.persist(project,write);
  }
  async publishArena(project:Project,write=writeDiskProject,publish=publishDiskArena){
    if(this.workspace!=='arena')throw new Error('只有 Arena 工作区可以发布正式内容');
    if(this.busy)throw new Error(this.saving?'正在保存，请等待当前写入完成':'正在发布，请等待当前操作完成');
    this.publishing=true;
    try {
      if(this.dirty(project))await this.persist(project,write);
      return await publish(this.etag);
    } finally {this.publishing=false;}
  }
  private async persist(project:Project,write:typeof writeDiskProject){
    const snapshot=clone(project);const key=JSON.stringify(snapshot);
    this.saving=true;this.failure='';
    try{this.etag=await write(snapshot,this.etag,this.workspace);this.savedKey=key;}
    catch(error){this.failure=String(error);throw error;}
    finally{this.saving=false;}
  }
}
