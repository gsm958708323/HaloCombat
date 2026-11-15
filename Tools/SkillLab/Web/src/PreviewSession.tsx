import type {AuthorNodeSource,Project} from './model';
import {usePreview} from './usePreview';
import {Preview} from './Preview';

// Simulation frames belong to this subtree; authoring does not rerender on every snapshot.
export function PreviewSession({project,root,report,update,inspectSource,adoptReplay}:{project:Project;root:string;report:(message:string)=>void;
  update:(project:Project)=>void;inspectSource:(source:AuthorNodeSource)=>void;adoptReplay:(project:Project,root:string)=>void}) {
  const preview=usePreview(project,root,report,adoptReplay);preview.skill.current=root;
  return <Preview project={project} root={root} preview={preview} update={update} inspectSource={inspectSource}/>;
}
