import './inspector.css';
import {NodeParameters} from './NodeParameters';
import type {EditorNode} from './SkillNode';
import {kinds} from './model';

export function NodeInspector({node,count}:{node?:EditorNode;count:number}) {
  return <aside className="properties node-inspector" aria-label="选中节点属性"><h3>节点属性</h3>{node?<>
    <h4>{kinds[node.data.kind]?.title??node.data.kind}</h4><small className="node-identity">{node.id}</small>
    {count>1&&<p>已选 {count} 个节点，显示最后选中的节点。</p>}
    <NodeParameters key={node.id} data={node.data}/>
  </>:<p className="runtime-empty">点击画布中的节点，查看和编辑它的属性。</p>}</aside>;
}
