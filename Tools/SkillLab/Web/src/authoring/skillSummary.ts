import {burstEnabled,deliveryLabel,kinds,nodeOutputs,parameterValue,effectKinds} from '../model';
import type {Field,GraphNode,GraphEdge,Project,Skill} from '../model';
import {formatFieldValue} from './numericParameters';
import {nodeFieldGroups} from './nodePresentation';

type Graph={id:string;nodes:GraphNode[];edges:GraphEdge[];root?:string};
interface DraftItem {key:string;node:GraphNode;labels:string[];references:number;authoredTimeline:boolean;definitionRoot:boolean;behaviorLinked:boolean;warning?:string}
export interface SummaryProperty {key:string;label:string;value:string}
export interface SummaryItem {key:string;title:string;labels:string[];references:number;main:SummaryProperty[];more:SummaryProperty[];warning?:string}
export function skillSummary(project:Project,skill:Skill) {
  const items:DraftItem[]=[],seen=new Map<string,DraftItem>(),warnings:string[]=[];
  function walk(graph:Graph,kind:string,id:string,label:string,authored=false,path=new Set<string>()) {
    const node=graph.nodes.find(n=>n.id===id);if(!node||node.enabled===false)return;
    const key=kind+':'+graph.id+'/'+id;
    if(path.has(key)){const previous=seen.get(key);if(previous)previous.warning='此分支存在循环引用，请修复后运行';return;}
    const previous=seen.get(key);
    if(previous){previous.references++;if(!previous.labels.includes(label))previous.labels.push(label);return;}
    const behavior=graph.edges.find(e=>e.target===id&&e.targetPort==='behavior'&&graph.nodes.find(n=>n.id===e.source)?.enabled!==false);
    const row:DraftItem={key,node,labels:[label],references:1,authoredTimeline:authored,definitionRoot:graph.root===id,behaviorLinked:!!behavior};items.push(row);seen.set(key,row);
    const nextPath=new Set(path);nextPath.add(key);
    if(node.kind==='projectile-behavior')return;
    if(behavior)walk(graph,kind,behavior.source,'投射物行为',false,nextPath);
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
      const transfer=graph.nodes.find(n=>n.id===edge.target);
      const branch=edge.sourcePort==='cast'&&transfer&&['projectile','projectile-ref','area-ref','delivery'].includes(transfer.kind)?'传递':edge.sourcePort==='next'||edge.sourcePort==='cast'?label:nodeOutputs(node).find(([port])=>port===edge.sourcePort)?.[1]??label;
      walk(graph,kind,edge.target,branch,false,nextPath);
    }
  }
  const target=skill.nodes.find(n=>n.kind==='target'&&n.enabled!==false),cast=skill.nodes.find(n=>n.kind==='cast'&&n.enabled!==false);
  for(const [kind,label] of [['target','目标选择'],['cast','施放规则']])if(skill.nodes.filter(n=>n.kind===kind&&n.enabled!==false).length!==1)warnings.push('需要且只能有一个启用的'+label+'节点');
  if(target)walk({id:skill.id,nodes:[target],edges:[]},'skill',target.id,'目标');
  if(cast)walk(skill,'skill',cast.id,'施放',!!(skill.timeline||skill.timelineRef));
  const shared=skill.timelineRef?project.timelines?.find(t=>t.id===skill.timelineRef):undefined;
  if(skill.timelineRef&&!shared)warnings.push('缺少共享时间轴：'+skill.timelineRef);
  const timeline=shared?.timeline??skill.timeline,owner=shared??skill;
  if(timeline&&cast) {
    const entries=[...timeline.payloads.map(p=>({time:p.time,effects:p.effects})),...timeline.clips.filter(c=>c.kind==='hitbox').map(c=>({time:c.start,effects:c.effects??[]}))].sort((a,b)=>a.time-b.time);
    for(const entry of entries)for(const id of entry.effects)walk(owner,shared?'timeline':'skill',id,'动作触发');
  }
  for(const event of skill.nodes.filter(n=>n.kind==='skill-events'))walk(skill,'skill',event.id,'技能事件');
  const mode=String(cast?.parameters.mode??'instant'),operation=cast?.parameters.execution==='operation';
  const casting=operation?'即时操作':mode==='channel'?'按住引导，松开停止':mode==='charge'?'保持蓄力，松开释放':burstEnabled(cast?.parameters??{})?'有限连发 '+Number(cast?.parameters.count??3)+' 次':'瞬发';
  const targeting=target?.parameters.mode==='direction'?'每次触发独立抽取方向':target?.parameters.mode==='random'?'每次触发随机选择当前合法目标':target?({point:'朝指定位置施放',entity:'作用于指定目标',nearest:'选择最近合法目标',self:'作用于施法者自身',facing:'朝当前方向施放'} as Record<string,string>)[String(target.parameters.mode??'point')]:'';
  const transfer=items.find(r=>['projectile','delivery','construct','teleport-projectile'].includes(r.node.kind));
  const title=(node:GraphNode)=>node.kind==='projectile-behavior'?(node.parameters.mode==='chain'?'连锁行为':'穿透行为'):effectKinds.find(([kind])=>kind===node.kind)?.[1]??kinds[node.kind]?.title??node.kind;
  const settlement=items.filter(r=>['damage','buff','heal','dispel','split'].includes(r.node.kind)).map(r=>title(r.node)).filter((v,i,a)=>a.indexOf(v)===i).join('、');
  const behavior=items.find(r=>r.node.kind==='projectile-behavior')?.node;
  const projectile=items.find(r=>r.node.kind==='projectile')?.node;
  const chain=behavior?.parameters.mode==='chain'?behavior.parameters.chainCount??3:!behavior&&projectile?.parameters.chain?projectile.parameters.chainCount??2:null;
  const unlimited=behavior?.parameters.mode==='pierce'?behavior.parameters.unlimited===true:!behavior&&projectile?.parameters.maxHits===0;
  const damageKinds=[...new Set(items.filter(r=>r.node.kind==='damage').map(r=>r.node.parameters.damageType==='elemental'?'通用元素':'物理'))];
  const description=cast?[casting,targeting,transfer?deliveryLabel(transfer.node):'',chain!=null?'额外连锁'+chain+'次':unlimited?'无限穿透':'',settlement?'包含'+settlement:'',damageKinds.length?'造成'+damageKinds.join('和')+'伤害':''].filter(Boolean).join('，')+'。':'当前图缺少有效施放规则。';
  function property(field:Field,node:GraphNode):SummaryProperty {
    const raw=parameterValue(field,node.parameters),catalog=field.key==='buff'?project.buffs:field.key==='projectile'?project.projectiles:field.key==='area'?project.areas:field.key==='blueprint'?project.summonBlueprints:undefined;
    return {key:field.key,label:field.label,value:(catalog?.find(d=>d.id===raw)?.name??formatFieldValue(field,raw))+(field.unit??'')};
  }
  const rows:SummaryItem[]=items.map(item=>{
    const fields=nodeFieldGroups(item.node,{authoredTimeline:item.authoredTimeline,definitionRoot:item.definitionRoot,behaviorLinked:item.behaviorLinked}),main=fields.main.map(f=>property(f,item.node));
    const more=[...fields.behaviors,...fields.advanced].map(f=>property(f,item.node));let warning=item.warning;
    const buff=item.node.kind==='buff'?project.buffs?.find(b=>b.id===item.node.parameters.buff):null;
    if(buff){main.push({key:'buff-duration',label:'持续时间',value:buff.permanent?'永久':buff.duration+'秒'},{key:'buff-stack',label:'叠层策略',value:({refresh:'刷新时长',add:'增层并刷新',independent:'独立实例',reject:'已有则拒绝'} as Record<string,string>)[buff.stack]??buff.stack},{key:'buff-max',label:'最大层数',value:String(buff.maxStacks)});if(buff.interval>0)main.push({key:'buff-interval',label:'周期',value:buff.interval+'秒'});
      more.push({key:'buff-tags',label:'授予 Tag',value:buff.tags.length?buff.tags.join(' / '):'无'});
      for(const [index,m] of buff.modifiers.entries())more.push({key:'buff-modifier-'+index,label:'属性修正 · '+m.attribute,value:({add:'加值 ',mul:'乘值 ',override:'覆盖 '} as Record<string,string>)[m.operation]+m.value});
    }else if(item.node.kind==='buff')warning='缺少Buff定义：'+String(item.node.parameters.buff??'未选择');
    if(item.node.kind==='delivery'&&item.node.parameters.mode==='summon'){
      const blueprint=project.summonBlueprints?.find(b=>b.id===item.node.parameters.blueprint);
      if(!blueprint)warning='缺少召唤蓝图：'+String(item.node.parameters.blueprint??'未选择');
      else{
        main.push({key:'blueprint-hp',label:'召唤生命',value:String(blueprint.hp)},{key:'blueprint-attack',label:'召唤攻击',value:String(blueprint.attack)},{key:'blueprint-skill',label:'攻击技能',value:project.skills.find(s=>s.id===blueprint.skillId)?.name??blueprint.skillId});
        more.push({key:'blueprint-defense',label:'召唤防御',value:String(blueprint.defense)},{key:'blueprint-speed',label:'召唤移速',value:blueprint.moveSpeed+'米/秒'},{key:'blueprint-energy',label:'初始 / 上限能量',value:(blueprint.initialEnergy??blueprint.energy)+' / '+blueprint.energy});
      }
    }
    return {key:item.key,title:title(item.node),labels:[...item.labels],references:item.references,warning,main,more};
  });
  return {items:rows,description,warnings};
}
