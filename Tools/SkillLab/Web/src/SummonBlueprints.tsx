import type { Project, SummonBlueprint } from './model';
import type { ContentAction } from './ContentDialog';

const numbers: [keyof SummonBlueprint,string][]=[['hp','生命'],['attack','攻击'],['defense','防御'],['moveSpeed','移速'],['collisionRadius','净空半径'],
  ['energy','能量上限'],['initialEnergy','初始能量'],['recovery','每秒恢复'],['ammo','弹药上限'],['initialAmmo','初始弹药'],['acquireRadius','索敌距离'],['attackRange','施放距离'],['followRange','跟随距离'],['holdDuration','保持时长']];

export function SummonBlueprints({project,update,action,duplicate}:{project:Project;update:(project:Project)=>void;action:(action:ContentAction)=>void;duplicate:(id:string)=>void}){
  const blueprints=project.summonBlueprints??[];
  function change(items:SummonBlueprint[]){update({...project,summonBlueprints:items});}
  function field(id:string,key:keyof SummonBlueprint,value:string|number){change(blueprints.map(b=>b.id===id?{...b,[key]:value}:b));}
  function add(){change([...blueprints,{id:crypto.randomUUID(),name:'新召唤蓝图',skillId:project.skills[0].id,hp:100,attack:80,defense:0,moveSpeed:3,collisionRadius:.4,
    energy:100,initialEnergy:100,recovery:10,acquireRadius:20,attackRange:12,followRange:2,holdDuration:.75}]);}
  return <details className="blueprint-panel"><summary>召唤蓝图 · {blueprints.length}</summary><button onClick={add}>添加蓝图</button>
    {blueprints.map((blueprint,index)=>{
      return <fieldset key={blueprint.id} data-blueprint-id={blueprint.id}><legend>{blueprint.name||'蓝图 '+(index+1)}</legend>
        <button className="content-id" title={blueprint.id} onClick={()=>action({kind:'blueprint',id:blueprint.id,mode:'rename'})}>ID {blueprint.id}</button>
        <label>名称<input aria-label={`蓝图${index+1}名称`} value={blueprint.name??''} onChange={e=>field(blueprint.id,'name',e.target.value)}/></label>
        <label>攻击技能<select aria-label={`蓝图${index+1}攻击技能`} value={blueprint.skillId} onChange={e=>field(blueprint.id,'skillId',e.target.value)}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
        <div className="blueprint-fields">{numbers.map(([key,label])=><label key={key}>{label}<input aria-label={`蓝图${index+1}${label}`} type="number" min={key==='hp'?.01:0} step={key==='ammo'||key==='initialAmmo'?'1':'.1'} value={blueprint[key]??(key==='initialAmmo'?blueprint.ammo??0:key==='ammo'?0:blueprint.energy)}
          onChange={e=>{if(e.target.value!=='')field(blueprint.id,key,Number(e.target.value));}}/></label>)}</div>
        <button onClick={()=>duplicate(blueprint.id)}>复制蓝图</button><button onClick={()=>action({kind:'blueprint',id:blueprint.id,mode:'delete'})}>移除蓝图</button>
      </fieldset>;
    })}
  </details>;
}
