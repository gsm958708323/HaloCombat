import {burstEnabled,deliveryLabel,kinds,nodeOutputs,parameterValue,effectKinds} from '../model';
import type {Field,GraphNode,GraphEdge,Project,Skill} from '../model';
import {formatFieldValue} from './numericParameters';
import {nodeFieldGroups} from './nodePresentation';

type Graph={id:string;nodes:GraphNode[];edges:GraphEdge[];root?:string};
interface DraftItem {key:string;node:GraphNode;labels:string[];references:number;authoredTimeline:boolean;definitionRoot:boolean;warning?:string}
export interface SummaryProperty {key:string;label:string;value:string}
export interface SummaryItem {key:string;title:string;labels:string[];references:number;main:SummaryProperty[];more:SummaryProperty[];warning?:string}
export function skillSummary(project:Project,skill:Skill) {
  const items:DraftItem[]=[],seen=new Map<string,DraftItem>();
  function walk(graph:Graph,kind:string,id:string,label:string,authored=false,path=new Set<string>()) {
    const node=graph.nodes.find(n=>n.id===id);if(!node||node.enabled===false)return;
    const key=kind+':'+graph.id+'/'+id;
    if(path.has(key)){const previous=seen.get(key);if(previous)previous.warning='此分支存在循环引用，请修复后运行';return;}
    const previous=seen.get(key);
    if(previous){previous.references++;if(!previous.labels.includes(label))previous.labels.push(label);return;}
    const row:DraftItem={key,node,labels:[label],references:1,authoredTimeline:authored,definitionRoot:graph.root===id};items.push(row);seen.set(key,row);
    const nextPath=new Set(path);nextPath.add(key);
    if(node.kind==='projectile-ref'||node.kind==='area-ref') {
      const area=node.kind==='area-ref',definitions=area?project.areas:project.projectiles,ref=String(node.parameters[area?'area':'projectile']??'');
      const definition=definitions?.find(d=>d.id===ref);
      if(definition)walk(definition,area?'area':'projectile',definition.root,label,false,nextPath);
      else row.warning='缺少'+(area?'区域':'弹体')+'定义：'+ref;
    }
    const ports=nodeOutputs(node).map(([port])=>port);
    const edges=graph.edges.filter(e=>e.source===id).sort((a,b)=>ports.indexOf(a.sourcePort)-ports.indexOf(b.sourcePort)||a.order-b.order);
    for(const edge of edges) {
      if(!ports.includes(edge.sourcePort))continue;
      const branch=edge.sourcePort==='next'||edge.sourcePort==='cast'?label:nodeOutputs(node).find(([port])=>port===edge.sourcePort)?.[1]??label;
      walk(graph,kind,edge.target,branch,false,nextPath);
    }
  }
  const target=skill.nodes.find(n=>n.kind==='target'),cast=skill.nodes.find(n=>n.kind==='cast');
  if(target)walk({id:skill.id,nodes:[target],edges:[]},'skill',target.id,'目标');
  if(cast)walk(skill,'skill',cast.id,'施放',!!(skill.timeline||skill.timelineRef));
  const shared=skill.timelineRef?project.timelines?.find(t=>t.id===skill.timelineRef):undefined;
  const timeline=shared?.timeline??skill.timeline,owner=shared??skill;
  if(timeline) {
    const entries=[...timeline.payloads.map(p=>({time:p.time,effects:p.effects})),...timeline.clips.filter(c=>c.kind==='hitbox').map(c=>({time:c.start,effects:c.effects??[]}))].sort((a,b)=>a.time-b.time);
    for(const entry of entries)for(const id of entry.effects)walk(owner,shared?'timeline':'skill',id,'动作触发');
  }
  for(const event of skill.nodes.filter(n=>n.kind==='skill-events'))walk(skill,'skill',event.id,'技能事件');
  const mode=String(cast?.parameters.mode??'instant'),operation=cast?.parameters.execution==='operation';
  const casting=operation?'即时操作':mode==='channel'?'按住引导，松开停止':mode==='charge'?'保持蓄力，松开释放':burstEnabled(cast?.parameters??{})?'有限连发 '+Number(cast?.parameters.count??3)+' 次':'瞬发';
  const targeting=target?.parameters.mode==='direction'?'每次触发独立抽取方向':target?.parameters.mode==='random'?'每次触发随机选择当前合法目标':'';
  const transfer=items.find(r=>['projectile','delivery','construct','teleport-projectile'].includes(r.node.kind));
  const title=(node:GraphNode)=>effectKinds.find(([kind])=>kind===node.kind)?.[1]??kinds[node.kind]?.title??node.kind;
  const settlement=items.filter(r=>['damage','buff','heal','dispel','split'].includes(r.node.kind)).map(r=>title(r.node)).filter((v,i,a)=>a.indexOf(v)===i).join('、');
  const description=[casting,targeting,transfer?deliveryLabel(transfer.node):'',settlement?'包含'+settlement:''].filter(Boolean).join('，')+'。';
  function property(field:Field,node:GraphNode):SummaryProperty {
    const raw=parameterValue(field,node.parameters),catalog=field.key==='buff'?project.buffs:field.key==='projectile'?project.projectiles:field.key==='area'?project.areas:field.key==='blueprint'?project.summonBlueprints:undefined;
    return {key:field.key,label:field.label,value:(catalog?.find(d=>d.id===raw)?.name??formatFieldValue(field,raw))+(field.unit??'')};
  }
  const rows:SummaryItem[]=items.map(item=>{
    const fields=nodeFieldGroups(item.node,{authoredTimeline:item.authoredTimeline,definitionRoot:item.definitionRoot}),main=fields.main.map(f=>property(f,item.node));
    const buff=item.node.kind==='buff'?project.buffs?.find(b=>b.id===item.node.parameters.buff):null;
    if(buff){main.push({key:'buff-duration',label:'持续时间',value:buff.permanent?'永久':buff.duration+'秒'});if(buff.interval>0)main.push({key:'buff-interval',label:'周期',value:buff.interval+'秒'});}
    return {key:item.key,title:title(item.node),labels:[...item.labels],references:item.references,warning:item.warning,
      main,more:[...fields.behaviors,...fields.advanced].map(f=>property(f,item.node))};
  });
  return {items:rows,description};
}
