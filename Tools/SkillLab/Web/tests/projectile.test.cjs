const test=require('node:test'),assert=require('node:assert/strict'),path=require('node:path');
const {readProject}=require('./projectFixture.cjs');
const {connectGraph,copySelection,pasteSelection,deleteSelection}=require('../../.local/web-checks/authoring/graphEdits.js');
const {duplicateContent}=require('../../.local/web-checks/authoring/contentEdits.js');
const {extractProjectile}=require('../../.local/web-checks/authoring/projectileEdits.js');
const {nodeFieldGroups,nodePortGroups}=require('../../.local/web-checks/authoring/nodePresentation.js');
const {skillSummary}=require('../../.local/web-checks/authoring/skillSummary.js');
const example=()=>readProject(path.resolve(__dirname,'../../../../Content/SkillLab/project.json'));
const key={kind:'skill',id:'chain-shot'};
test('chain and piercing are executable effect nodes reached through actual hit edges',()=>{
  const p=example(),g=p.skills.find(s=>s.id===key.id),chain=g.nodes.find(n=>n.kind==='chain'),body=g.nodes.find(n=>n.kind==='projectile');
  assert(chain);assert(g.edges.some(e=>e.source===body.id&&e.sourcePort==='hit'&&e.target===chain.id&&e.targetPort==='execute'));
  assert.equal(body.parameters.chain,undefined);assert.equal(body.parameters.maxHits,undefined);
  const disconnected={...g,edges:g.edges.filter(e=>e.target!==chain.id)};
  const connected=connectGraph(disconnected,{source:body.id,sourceHandle:'hit',target:chain.id,targetHandle:'execute'},'restored');
  assert(connected.edges.some(e=>e.id==='restored'));
  assert.throws(()=>connectGraph(connected,{source:chain.id,sourceHandle:'next',target:body.id,targetHandle:'execute'}),/循环/);
});
test('deleting and disabling an effect remove it from the effective skill summary',()=>{
  const p=example(),g=p.skills.find(s=>s.id===key.id),chain=g.nodes.find(n=>n.kind==='chain');
  const gone=deleteSelection(p,key,[chain.id],[]);assert(!skillSummary(gone,gone.skills.find(s=>s.id===key.id)).items.some(i=>i.title==='连锁效果'));
  chain.enabled=false;assert(!skillSummary(p,g).items.some(i=>i.title==='连锁效果'));
});
test('shared projectile extraction and skill copy preserve contact effects with separate identities',()=>{
  const p=example(),g=p.skills.find(s=>s.id===key.id),body=g.nodes.find(n=>n.kind==='projectile'),chain=g.nodes.find(n=>n.kind==='chain');let id=0;
  const copy=duplicateContent(p,'skill',g.id,'copy',()=>String(++id));const copied=copy.project.skills.at(-1);assert(copied.nodes.some(n=>n.kind==='chain'&&n.id!==chain.id));
  const extracted=extractProjectile(p,key,body.id,()=>String(++id));const definition=extracted.project.projectiles.at(-1);
  assert(definition.nodes.some(n=>n.kind==='chain'));assert(definition.edges.some(e=>e.sourcePort==='hit'&&e.targetPort==='execute'));
  const pasted=pasteSelection(p,key,copySelection(g,[chain.id]),{x:50,y:50},()=>String(++id));assert(pasted.project.skills.find(s=>s.id===g.id).nodes.filter(n=>n.kind==='chain').length===2);
});
test('conditional fields hide inactive piercing limits and irrelevant damage parameters',()=>{
  const node={id:'pierce',kind:'pierce',parameters:{maxHits:0},position:{x:0,y:0}};
  assert(!nodeFieldGroups(node).main.some(f=>f.key==='maxHits'));node.parameters.maxHits=3;assert(nodeFieldGroups(node).main.some(f=>f.key==='maxHits'));
  const damage={...node,kind:'damage',parameters:{canCrit:false,includeBonuses:false}};const fields=nodeFieldGroups(damage).main;
  assert(!fields.some(f=>['critChance','critMultiplier','coefficient'].includes(f.key)));
});
test('flight clock fields follow their selected mode and connected ports remain visible',()=>{
  const body=example().skills[0].nodes.find(n=>n.kind==='projectile');
  for(const mode of ['none','time','distance']){
    const node={...body,parameters:{...body.parameters,flightTrigger:mode}},fields=nodeFieldGroups(node).main;
    assert.equal(fields.some(f=>f.key==='flightInterval'),mode==='time');assert.equal(fields.some(f=>f.key==='flightDistance'),mode==='distance');
    assert.equal(nodePortGroups(node).main.some(([p])=>p==='flight'),mode!=='none');
  }
  assert(nodePortGroups(body,{flight:1}).main.some(([p])=>p==='flight'));
});
