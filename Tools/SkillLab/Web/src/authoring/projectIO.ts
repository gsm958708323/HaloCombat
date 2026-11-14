import { clone } from '../model';
import type { Project } from '../model';

export interface DiskProject { project:Project;etag:string }
export async function readDiskProject():Promise<DiskProject> {
  const response=await fetch('/api/project',{cache:'no-store'});
  if(!response.ok)throw new Error('无法读取磁盘工程：'+response.status);
  return {project:await response.json(),etag:response.headers.get('ETag')??''};
}
export async function writeDiskProject(project:Project,etag:string):Promise<string> {
  const response=await fetch('/api/project',{method:'POST',headers:{'Content-Type':'application/json','If-Match':etag},body:JSON.stringify(project)});
  if(!response.ok){const data=await response.json();throw new Error(data.error??data.detail??'保存失败');}
  return response.headers.get('ETag')??'';
}
// Acknowledgement applies to the captured snapshot, never to edits made during the request.
export class ProjectSave {
  saving=false;
  failure='';
  private savedKey:string;
  private etag:string;
  constructor(project:Project,etag:string){this.savedKey=JSON.stringify(project);this.etag=etag;}
  dirty(project:Project){return this.savedKey!==JSON.stringify(project);}
  async save(project:Project,write=writeDiskProject){
    if(this.saving)throw new Error('正在保存，请等待当前写入完成');
    const snapshot=clone(project);const key=JSON.stringify(snapshot);
    this.saving=true;this.failure='';
    try{this.etag=await write(snapshot,this.etag);this.savedKey=key;}
    catch(error){this.failure=String(error);throw error;}
    finally{this.saving=false;}
  }
}
