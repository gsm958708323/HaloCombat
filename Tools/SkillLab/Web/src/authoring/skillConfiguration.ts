import type {Project,Skill} from '../model';

export function currentSkillJson(project:Project,id:string):string {
  const skill=project.skills.find(s=>s.id===id);if(!skill)return '';
  return JSON.stringify({...skill,nodes:skill.nodes.map(({position,...node})=>({...node,
    runtimeId:node.runtimeId||undefined,enabled:node.enabled===false?false:undefined}))},null,2);
}

export function applySkillConfiguration(project:Project,id:string,text:string):{project:Project;json:string} {
  const previous=project.skills.find(s=>s.id===id);if(!previous)throw new Error('当前技能已不存在');
  const value=JSON.parse(text) as Skill;
  if(!value||value.id!==id||value.runtimeId!==previous.runtimeId)throw new Error('请保留当前技能的 id/runtimeId；改名请使用技能 ID 入口');
  if(!Array.isArray(value.nodes)||!Array.isArray(value.edges))throw new Error('当前技能需要 nodes 和 edges 数组');
  const next={...value,nodes:value.nodes.map((node,index)=>({...node,position:node.position??previous.nodes.find(n=>n.id===node.id)?.position??{x:index*300,y:80}}))};
  // Keep raw text in the validation payload so duplicate keys still reach C# validation.
  const json=JSON.stringify({...project,skills:undefined}).slice(0,-1)+',"skills":['+
    project.skills.map(s=>s.id===id?text:JSON.stringify(s)).join(',')+']}';
  return {project:{...project,skills:project.skills.map(s=>s.id===id?next:s)},json};
}
