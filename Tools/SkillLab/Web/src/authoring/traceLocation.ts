import type {AuthorNodeSource,Project} from '../model';
import {graphOf} from './contentEdits';
import type {GraphKey} from './contentEdits';

export function traceLocation(project:Project,source:AuthorNodeSource):GraphKey {
  if(!['skill','buff','timeline','projectile','area'].includes(source.graphKind))throw new Error('运行来源的图类型不受支持');
  const key={kind:source.graphKind,id:source.graphId} as GraphKey;
  const graph=graphOf(project,key),node=graph?.nodes.find(n=>n.id===source.nodeId);
  if(!node||node.enabled===false||node.kind!==source.nodeKind)throw new Error('运行来源节点已被修改或删除，请重新应用配置');
  return key;
}
