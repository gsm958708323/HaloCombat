import type {AuthorNodeSource,Project} from './model';
import {usePreview} from './usePreview';
import {Preview} from './Preview';
import {useEffect} from 'react';
import type {PreviewRuntimeStore} from './previewRuntime';

// Simulation frames belong to this subtree; authoring does not rerender on every snapshot.
export function PreviewSession({project,root,report,update,inspectSource,adoptReplay,store}:{project:Project;root:string;report:(message:string)=>void;
  update:(project:Project)=>void;inspectSource:(source:AuthorNodeSource)=>void;adoptReplay:(project:Project,root:string)=>void;store:PreviewRuntimeStore}) {
  const preview=usePreview(project,root,report,adoptReplay);preview.skill.current=root;
  useEffect(()=>{store.publish({state:preview.state,ready:preview.ready,send:input=>void preview.input(input),playing:preview.playing,play:()=>preview.setPlaying(true)});},[preview.state,preview.playing,preview.ready,store]);
  return <Preview project={project} root={root} preview={preview} update={update} inspectSource={inspectSource}/>;
}
