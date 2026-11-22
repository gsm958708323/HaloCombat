import { clone } from '../model';
import type { Project } from '../model';
import { prepareImportedLayout } from './importLayout';

export interface DiskProject { project:Project;etag:string }
const projectURL='/api/project';
export async function readDiskProject():Promise<DiskProject> {
  const response=await fetch(projectURL,{cache:'no-store'});
  if(!response.ok){const data=await response.json().catch(()=>null);throw new Error(data?.error??'无法读取磁盘工程：'+response.status);}
  const etag=response.headers.get('ETag');
  if(!etag)throw new Error('磁盘工程响应缺少版本标记');
  const project:Project=await response.json();
  return {project:response.headers.get('X-Needs-Layout')==='true'?prepareImportedLayout(project):project,etag};
}
export async function writeDiskProject(project:Project,etag:string):Promise<string> {
  const response=await fetch(projectURL,{method:'POST',headers:{'Content-Type':'application/json','If-Match':etag},body:JSON.stringify(project)});
  if(!response.ok){const data=await response.json().catch(()=>null);throw new Error(data?.error??data?.detail??'保存失败：'+response.status);}
  const updated=response.headers.get('ETag');
  if(!updated)throw new Error('保存响应缺少版本标记，请重新载入磁盘工程');
  return updated;
}
export async function publishDiskArena(etag:string):Promise<{message:string}> {
  const response=await fetch('/api/runtime/publish',{method:'POST',headers:{'If-Match':etag}});
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
  constructor(project:Project,etag:string){this.savedKey=JSON.stringify(project);this.etag=etag;}
  dirty(project:Project){return this.savedKey!==JSON.stringify(project);}
  get busy(){return this.saving||this.publishing;}
  async save(project:Project,write=writeDiskProject){
    if(this.busy)throw new Error(this.saving?'正在保存，请等待当前写入完成':'正在发布，请等待当前操作完成');
    await this.persist(project,write);
  }
  async publishArena(project:Project,write=writeDiskProject,publish=publishDiskArena){
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
    try{this.etag=await write(snapshot,this.etag);this.savedKey=key;}
    catch(error){this.failure=String(error);throw error;}
    finally{this.saving=false;}
  }
}
