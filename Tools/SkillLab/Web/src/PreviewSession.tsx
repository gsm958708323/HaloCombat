import type {AuthorNodeSource,Project} from './model';
import {usePreview} from './usePreview';
import {Preview} from './Preview';
import {useLayoutEffect} from 'react';
import type {PreviewRuntimeStore} from './previewRuntime';

// Simulation frames belong to this subtree; authoring does not rerender on every snapshot.
export function PreviewSession({project,root,report,update,inspectSource,adoptReplay,store,mode}:{project:Project;root:string;mode:'skill'|'combo';report:(message:string)=>void;
  update:(project:Project)=>void;inspectSource:(source:AuthorNodeSource)=>void;adoptReplay:(project:Project,root:string)=>void;store:PreviewRuntimeStore}) {
  const preview=usePreview(project,root,report,adoptReplay,mode==='skill');preview.skill.current=root;
  useLayoutEffect(()=>()=>store.publish(null),[store]);
  useLayoutEffect(()=>{if(mode==='combo')store.publish(preview.state);},[mode,preview.state,store]);
  return <Preview mode={mode} project={project} root={root} preview={preview} update={update} inspectSource={inspectSource}/>;
}
