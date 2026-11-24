import type { Project } from '../model';
import { prepareImportedLayout } from './importLayout';

export interface DetachedReference {
  path:string;
  nodeId:string;
  field:string;
  originalId:string;
}
export interface ImportPayload {
  json:string;
  needsLayout:boolean;
  detachedReferences?:DetachedReference[];
}
export interface ImportCandidate {
  project:Project;
  detachedReferences:DetachedReference[];
}
export function prepareImport(result:ImportPayload):ImportCandidate {
  const project=JSON.parse(result.json) as Project;
  return {
    project: result.needsLayout?prepareImportedLayout(project):project,
    detachedReferences: result.detachedReferences??[]
  };
}
