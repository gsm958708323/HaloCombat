const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {nodeFieldGroups,nodePortGroups}=require('../../.local/web-checks/authoring/nodePresentation.js');
const {skillSummary}=require('../../.local/web-checks/authoring/skillSummary.js');
const {setBurst,setCastExecution}=require('../../.local/web-checks/authoring/castExecution.js');
const {burstEnabled,nodeFields,nodeOutputs}=require('../../.local/web-checks/model.js');
const runtime=()=>JSON.parse(fs.readFileSync(path.resolve(__dirname,'../../../../Assets/StreamingAssets/Combat/project.json'),'utf8'));
const examples=()=>JSON.parse(fs.readFileSync(path.resolve(__dirname,'../../../../Content/SkillLab/project.json'),'utf8'));

test('burst switching preserves configured count interval payment and identity across off/on',()=>{
  const node={id:'cast',kind:'cast',position:{x:0,y:0},parameters:{mode:'instant',count:20,interval:.04,costPerPulse:true}};
  const before=JSON.stringify(node),off=setBurst(node,false),on=setBurst(off,true);assert.equal(JSON.stringify(node),before);
  assert.equal(burstEnabled(off.parameters),false);assert.equal(off.parameters.count,20);assert.equal(on.parameters.count,20);assert.equal(on.parameters.interval,.04);assert.equal(on.parameters.costPerPulse,true);
  assert(!nodeFields(off).some(f=>f.key==='count'));assert(nodeFields(on).some(f=>f.key==='count'));
  assert.equal(setBurst({...node,parameters:{}},true).parameters.count,3);assert.equal(setCastExecution(on,'operation').parameters.burst,undefined);
});
test('main advanced and behavior groups preserve every applicable field exactly once',()=>{
  const p=examples();for(const node of p.skills.flatMap(s=>s.nodes)){
    const groups=nodeFieldGroups(node),all=[...groups.main,...groups.advanced,...groups.behaviors].map(f=>f.key);
    assert.deepEqual([...all].sort(),nodeFields(node).map(f=>f.key).sort());assert.equal(new Set(all).size,all.length);
  }
  const damage=p.skills[0].nodes.find(n=>n.kind==='damage'),groups=nodeFieldGroups(damage);
  assert(groups.main.some(f=>f.key==='flat'));assert(groups.advanced.some(f=>f.key==='snapshotAttack'));assert(groups.main.length<nodeFields(damage).length);
});
test('folded optional event ports never conceal a connected wire or change the available contract',()=>{
  const node=examples().skills[0].nodes.find(n=>n.kind==='projectile'),before=JSON.stringify(node);
  const groups=nodePortGroups(node,{lifetime:1}),all=[...groups.main,...groups.extra].map(([key])=>key);
  assert(groups.main.some(([key])=>key==='lifetime'));assert(groups.extra.some(([key])=>key==='returned'));
  assert.deepEqual(all.sort(),nodeOutputs(node).map(([key])=>key).sort());assert.equal(JSON.stringify(node),before);
});
test('shared projectile edits reach the summary through references without copying gameplay state',()=>{
  const p=runtime(),s=p.skills.find(s=>s.id==='arena-ammo-shot'),before=JSON.stringify(p);let summary=skillSummary(p,s);
  const row=summary.items.find(i=>i.key==='projectile:arena-normal-projectile/projectile');assert.equal(row.main.find(f=>f.key==='speed').value,'6米/秒');assert.equal(JSON.stringify(p),before);
  assert(!row.main.some(f=>f.key==='multi'),"Shared definition must not invent emission parameters owned by its caller.");
  p.projectiles.find(d=>d.id==='arena-normal-projectile').nodes[0].parameters.speed=9;summary=skillSummary(p,s);
  assert.equal(summary.items.find(i=>i.key===row.key).main.find(f=>f.key==='speed').value,'9米/秒');
  assert(!('node' in row));row.main[0].value='changed view';assert.notEqual(p.projectiles[0].nodes[0].parameters.speed,'changed view');
});
test('disabled branches and disconnected draft effects cannot masquerade as effective summary content',()=>{
  const p=examples(),s=p.skills[0],damage=s.nodes.find(n=>n.kind==='damage');damage.enabled=false;
  s.nodes.push({id:'unused-heal',kind:'heal',position:{x:0,y:0},parameters:{flat:999}});
  const summary=skillSummary(p,s);assert(!summary.items.some(i=>i.key.endsWith('/'+damage.id)));assert(!summary.items.some(i=>i.key.endsWith('/unused-heal')));
});
test('summary follows explicit edge order and keeps repeated authored entries as reference counts',()=>{
  const p=examples(),s=p.skills[0],damage=s.nodes.find(n=>n.kind==='damage'),shot=s.nodes.find(n=>n.kind==='projectile');
  s.nodes.push({id:'second',kind:'heal',position:{x:0,y:0},parameters:{flat:2}});
  s.edges.push({id:'shot-second',source:shot.id,sourcePort:'hit',target:'second',targetPort:'execute',order:10});
  s.edges.push({id:'repeat-damage',source:shot.id,sourcePort:'hit',target:damage.id,targetPort:'execute',order:20});
  const summary=skillSummary(p,s),d=summary.items.find(i=>i.key.endsWith('/'+damage.id));
  assert.equal(d.references,2);assert(summary.items.findIndex(i=>i===d)<summary.items.findIndex(i=>i.key.endsWith('/second')));
});
test('missing shared content and recursive drafts yield explicit local warnings',()=>{
  const p=runtime(),s=p.skills.find(s=>s.id==='arena-ammo-shot');p.projectiles=[];
  assert(skillSummary(p,s).items.some(i=>i.warning?.includes('缺少弹体')));
  const q=examples(),g=q.skills[0],d=g.nodes.find(n=>n.kind==='damage');g.edges.push({id:'cycle',source:d.id,sourcePort:'next',target:d.id,targetPort:'execute',order:0});
  assert(skillSummary(q,g).items.some(i=>i.warning?.includes('循环')));
});
