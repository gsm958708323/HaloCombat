import type {AuthorNodeSource,Project} from './model';
import {usePreview} from './usePreview';
import {Preview} from './Preview';
import {useEffect} from 'react';
import type {PreviewRuntimeStore} from './previewRuntime';

// Simulation frames belong to this subtree; authoring does not rerender on every snapshot.
export function PreviewSession({project,root,report,update,inspectSource,adoptReplay,store}:{project:Project;root:string;report:(message:string)=>void;
  update:(project:Project)=>void;inspectSource:(source:AuthorNodeSource)=>void;adoptReplay:(project:Project,root:string)=>void;store:PreviewRuntimeStore}) {
  const preview=usePreview(project,root,report,adoptReplay);preview.skill.current=root;
  useEffect(()=>{
    store.publish({state:preview.state,ready:preview.ready,playing:preview.playing,setPlaying:preview.setPlaying,
      send:(input,point)=>void preview.input(input,point),inputAndStep:(input,point)=>void preview.inputAndStep(input,point),
      step:()=>void preview.step(),reset:()=>void preview.reset()});
  },[preview.state,preview.ready,preview.playing,preview.reset,store]);
  return <Preview project={project} root={root} preview={preview} update={update} inspectSource={inspectSource}/>;
}
