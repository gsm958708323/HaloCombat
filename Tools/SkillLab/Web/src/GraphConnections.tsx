import { kinds, nodeOutputs } from './model';
import type { EditableGraph } from './authoring/contentEdits';
import { reorderEdge } from './authoring/graphEdits';

export function GraphConnections({graph,change}:{graph:EditableGraph;change:(update:(g:EditableGraph)=>EditableGraph)=>void}) {
  return <details className="connection-panel"><summary>连线与执行顺序 · {graph.edges.length}</summary>
    {graph.nodes.flatMap(node=>nodeOutputs(node).map(([port,label])=>{
      const edges=graph.edges.filter(e=>e.source===node.id&&e.sourcePort===port).sort((a,b)=>a.order-b.order);if(!edges.length)return null;
      return <section key={node.id+':'+port}><h4>{kinds[node.kind]?.title} / {label}</h4>{edges.map((edge,i)=><div className="connection-item" key={edge.id}>
        <span title={edge.id}>{i+1}. {kinds[graph.nodes.find(n=>n.id===edge.target)!.kind]?.title}<small>{edge.target}</small></span>
        <button aria-label={`上移连线${edge.id}`} disabled={i===0} onClick={()=>change(g=>reorderEdge(g,edge.id,-1))}>↑</button>
        <button aria-label={`下移连线${edge.id}`} disabled={i===edges.length-1} onClick={()=>change(g=>reorderEdge(g,edge.id,1))}>↓</button>
        <button aria-label={`删除连线${edge.id}`} onClick={()=>change(g=>({...g,edges:g.edges.filter(e=>e.id!==edge.id)}))}>×</button>
      </div>)}</section>;
    }))}
    {!graph.edges.length&&<p>从节点端口拖动连线。相同输出按此处顺序执行。</p>}
  </details>;
}
