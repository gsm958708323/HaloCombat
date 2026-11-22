import { kinds } from '../model';
import type { GraphNode, Parameters, ParameterValue } from '../model';

// The compiler uses absence for a disabled stack cost and only accepts an inner radius on a persistent area.
export function omitDefault(key:string,value:ParameterValue):boolean {
  return value===0&&(key==='buffStackCost'||key==='innerRadius'||key==='returnWindow') || key==='requiredBuff'&&value==='';
}

export function defaultNodeParameters(kind:string):Parameters {
  return Object.fromEntries(kinds[kind].fields.filter(field=>!omitDefault(field.key,field.initial)).map(field=>[field.key,field.initial]));
}

export function editNodeParameter(node:GraphNode,field:string,value:ParameterValue):GraphNode {
  const parameters={...node.parameters};
  if(omitDefault(field,value))delete parameters[field];
  else parameters[field]=value;
  if(node.kind==='cast'&&field==='requiredBuff'&&!value)delete parameters.buffStackCost;
  if(node.kind==='delivery'&&field==='persistent'&&value===false)delete parameters.innerRadius;
  return {...node,parameters};
}
