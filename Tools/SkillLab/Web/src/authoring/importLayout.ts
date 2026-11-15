import type {Project} from '../model';
import {allGraphs,updateGraph} from './contentEdits';
import {nodeFieldGroups,nodePortGroups} from './nodePresentation';

// Runtime files intentionally omit layout. Give their imported graph a readable view
// without changing the identities, edge order or any gameplay parameter.
export function prepareImportedLayout(project:Project):Project {
  let result=project;
  for(const {kind,graph} of allGraphs(project)) {
    if(!graph.nodes.length||!graph.nodes.every(n=>n.position.x===0&&n.position.y===0))continue;
    const levels=new Map<string,number>(),rows=new Map<number,number>();
    function depth(id:string,visiting=new Set<string>()):number {
      if(levels.has(id))return levels.get(id)!;if(visiting.has(id))return 0;const next=new Set(visiting);next.add(id);
      const value=Math.max(0,...graph.edges.filter(e=>e.target===id).map(e=>depth(e.source,next)+1));levels.set(id,value);return value;
    }
    result=updateGraph(result,{kind,id:graph.id},g=>({...g,nodes:g.nodes.map(n=>{
      const level=depth(n.id),y=rows.get(level)??60;
      const groups=nodeFieldGroups(n,{definitionRoot:n.id===g.root,authoredTimeline:!!(g.timeline||g.timelineRef)}),ports=nodePortGroups(n);
      const fields=groups.main.length+Number(groups.advanced.length>0)+Number(groups.behaviors.length>0);
      rows.set(level,y+Math.max(260,fields*38+ports.main.length*28+150));return {...n,position:{x:level*320,y}};
    })}));
  }
  return result;
}
