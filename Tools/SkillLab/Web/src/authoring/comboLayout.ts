import type {RuntimeCombo} from '../model';
import {comboStart} from './comboEdits';

export function layoutCombo(ids:string[],rows:RuntimeCombo[],root=ids[0]):Map<string,{x:number;y:number}>{
  const order=[comboStart,...ids],allowed=new Set(order),levels=new Map<string,number>([[comboStart,0]]);
  const children=new Map(order.map(id=>[id,[] as string[]]));
  for(const row of rows)for(const source of row.preSkills.length?row.preSkills:[comboStart])
    if(allowed.has(source)&&allowed.has(row.toSkill)&&!children.get(source)!.includes(row.toSkill))children.get(source)!.push(row.toSkill);
  function visit(seed:string){
    const pending=[seed];
    for(let index=0;index<pending.length;index++)for(const target of children.get(pending[index])??[]){
      if(levels.has(target))continue;levels.set(target,levels.get(pending[index])!+1);pending.push(target);
    }
  }
  visit(comboStart);
  // An action may be a legal predecessor without an idle entry for this input.
  // Place those source components separately; cycles use a deterministic seed.
  const incoming=new Set(rows.map(row=>row.toSkill));
  for(const id of [...ids.filter(id=>!incoming.has(id)),...ids])if(!levels.has(id)){levels.set(id,0);visit(id);}
  const columns=new Map<number,string[]>();
  for(const id of order){const level=levels.get(id)!;columns.set(level,[...(columns.get(level)??[]),id]);}
  // Reserve a visual row for the root and its same-input continuation. Alternate
  // inputs occupy branch rows. This is layout only and does not rank runtime rules.
  const principal=new Set<string>([comboStart]);
  const input=rows.find(row=>row.toSkill===root&&!row.preSkills.length)?.input
    ??rows.find(row=>row.preSkills.includes(root))?.input;
  let current=root;
  while(current&&allowed.has(current)&&!principal.has(current)){
    principal.add(current);
    const previousLevel=levels.get(current)!;
    current=rows.find(row=>row.input===input&&row.preSkills.includes(current)&&
      levels.get(row.toSkill)===previousLevel+1)?.toSkill??'';
  }
  const result=new Map<string,{x:number;y:number}>();
  for(const [column,items] of columns){
    let branch=1;
    let mainPlaced=false;
    for(const id of items){
      const onMain=principal.has(id)&&!mainPlaced;
      if(onMain)mainPlaced=true;
      result.set(id,{x:60+column*290,y:90+(onMain?0:branch++)*145});
    }
  }
  return result;
}
