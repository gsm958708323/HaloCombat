const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {switchDelivery,deliveryModes,deliveryMode,reconcileNodeOutputs}=require('../../.local/web-checks/authoring/deliveryEdits.js');
const {extractProjectileBehavior}=require('../../.local/web-checks/authoring/projectileBehavior.js');
const {graphOf,updateGraph}=require('../../.local/web-checks/authoring/contentEdits.js');
const {EditorHistory}=require('../../.local/web-checks/authoring/EditorHistory.js');
const {nodeFields,nodeOutputs}=require('../../.local/web-checks/model.js');
const examples=()=>JSON.parse(fs.readFileSync(path.resolve(__dirname,'../../../../Content/SkillLab/project.json'),'utf8'));
const key={kind:'skill',id:'basic-shot'};

test('one delivery control changes all seven modes without changing stable node or cast identities',()=>{
  for(const [mode] of deliveryModes){
    const p=examples(),graph=graphOf(p,key),original=graph.nodes.find(n=>n.kind==='projectile'),before=JSON.stringify(p);
    const result=switchDelivery(p,key,original.id,mode),node=graphOf(result.project,key).nodes.find(n=>n.id===original.id);
    assert.equal(JSON.stringify(p),before);assert.equal(node.id,original.id);assert.deepEqual(node.position,original.position);assert.equal(node.runtimeId,original.runtimeId);assert.equal(deliveryMode(node),mode);
    assert.deepEqual(graphOf(result.project,key).nodes.find(n=>n.kind==='cast'),graph.nodes.find(n=>n.kind==='cast'));
    const primary=mode==='projectile'?'hit':'apply';assert(graphOf(result.project,key).edges.some(e=>e.source===node.id&&e.sourcePort===primary&&e.target==='damage'));
    if(mode==='summon'){assert(node.parameters.blueprint);assert.notEqual(p.summonBlueprints.find(b=>b.id===node.parameters.blueprint).skillId,key.id);}
    if(mode==='area')assert.equal(node.parameters.radius,3,'Effect radius cannot reuse a projectile collision radius');
  }
});

test('mode changes keep relevant dimensions and remove fields from a different delivery domain',()=>{
  const p=examples(),id='strike',s=p.skills.find(s=>s.id==='melee-thrust'),node=s.nodes.find(n=>n.kind==='delivery'),k={kind:'skill',id:s.id};
  node.parameters.width=1.75;node.parameters.length=8;node.parameters.startup=.13;node.parameters.shape='thrust';
  let result=switchDelivery(p,k,node.id,'ray'),changed=graphOf(result.project,k).nodes.find(n=>n.id===node.id);
  assert.equal(changed.parameters.width,1.75);assert.equal(changed.parameters.length,8);assert.equal(changed.parameters.startup,undefined);assert.equal(changed.parameters.shape,undefined);
  result=switchDelivery(result.project,k,node.id,'projectile');changed=graphOf(result.project,k).nodes.find(n=>n.id===node.id);assert.equal(changed.parameters.width,undefined);assert.equal(changed.parameters.radius,.18);assert.equal(changed.parameters.sameTargetDelay,undefined);
  assert.equal(changed.runtimeId,node.runtimeId);assert(nodeFields(changed).every(f=>f.key in changed.parameters));
});

test('exclusive unsupported branches are retained disabled and one undo restores all parameters and edges',()=>{
  let p=examples();let n=0;p=updateGraph(p,key,g=>extractProjectileBehavior(g,'projectile',()=> 'branch-'+ ++n));
  p=updateGraph(p,key,g=>({...g,nodes:[...g.nodes,{id:'wall-cue',kind:'cue',position:{x:0,y:0},parameters:{id:1}}],edges:[...g.edges,{id:'wall',source:'projectile',sourcePort:'wall',target:'wall-cue',targetPort:'execute',order:0}]}));
  const before=structuredClone(p),history=new EditorHistory(p),result=switchDelivery(p,key,'projectile','melee');history.edit(result.project);
  const graph=graphOf(result.project,key);assert.equal(result.removedEdges,2);assert.equal(result.disabledNodes,2);assert.equal(graph.nodes.find(n=>n.id==='wall-cue').enabled,false);assert.equal(graph.nodes.find(n=>n.kind==='projectile-behavior').enabled,false);assert.notEqual(graph.nodes.find(n=>n.id==='damage').enabled,false);
  history.undo();assert.deepEqual(history.current,before);history.redo();assert.deepEqual(history.current,result.project);
});

test('a branch still called from another active entry is preserved when the old port disappears',()=>{
  let p=examples();p=updateGraph(p,key,g=>({...g,nodes:[...g.nodes,{id:'wall-cue',kind:'cue',position:{x:0,y:0},parameters:{id:1}}],edges:[...g.edges,{id:'wall',source:'projectile',sourcePort:'wall',target:'wall-cue',targetPort:'execute',order:0},{id:'also-cast',source:'cast',sourcePort:'cast',target:'wall-cue',targetPort:'execute',order:1}]}));
  const result=switchDelivery(p,key,'projectile','melee'),g=graphOf(result.project,key);assert.equal(result.disabledNodes,0);assert.notEqual(g.nodes.find(n=>n.id==='wall-cue').enabled,false);assert(g.edges.some(e=>e.id==='also-cast'));
});

test('shared roots and a split that would lose its hit context reject conversion without mutation',()=>{
  const p=examples(),shared=p.projectiles[0],before=JSON.stringify(p);assert.throws(()=>switchDelivery(p,{kind:'projectile',id:shared.id},shared.root,'area'),/根类型/);
  const s=p.skills.find(s=>s.id==='split-shot');assert.throws(()=>switchDelivery(p,{kind:'skill',id:s.id},s.nodes.find(n=>n.kind==='projectile').id,'melee'),/命中上下文/);assert.equal(JSON.stringify(p),before);
});

test('timeline entries retain their repeated scheduling identity when their delivery changes',()=>{
  const p=examples(),s=p.skills.find(s=>s.id==='controlled-shot'),k={kind:'skill',id:s.id},n=s.nodes.find(n=>n.kind==='projectile'),schedule=structuredClone(s.timeline);
  const result=switchDelivery(p,k,n.id,'ray');assert.deepEqual(graphOf(result.project,k).timeline,schedule);assert.equal(graphOf(result.project,k).nodes.find(v=>v.id===n.id).kind,'delivery');
});

test('all converted graphs are exported unchanged for authoritative C# compilation and real execution',()=>{
  const output=path.resolve(__dirname,'../../.local/verification/workbench-interactions');fs.mkdirSync(output,{recursive:true});
  for(const [mode] of deliveryModes){
    const p=examples(),s=graphOf(p,key);p.preview.caster.x=0;p.preview.caster.z=0;p.preview.targets=[{id:'target',x:2,z:0,hp:1000,atk:0,def:0}];p.preview.obstacles=[];p.preview.recovery=0;
    s.nodes.find(n=>n.kind==='target').parameters.mode='nearest';s.nodes.find(n=>n.kind==='cast').parameters.delay=0;s.nodes.find(n=>n.kind==='damage').parameters.canCrit=false;
    const result=switchDelivery(p,key,'projectile',mode);assert(graphOf(result.project,key).edges.every(e=>e.source!=='projectile'||nodeOutputs(graphOf(result.project,key).nodes.find(n=>n.id==='projectile')).some(([port])=>port===e.sourcePort)));
    fs.writeFileSync(path.join(output,'switch-'+mode+'.json'),JSON.stringify(result.project));
  }
});

test('turning off persistent area ports disables detached exclusive lifecycles and preserves the apply branch',()=>{
  const p=examples(),skill=p.skills.find(s=>s.id==='ground-field'),node=skill.nodes.find(n=>n.kind==='delivery');
  skill.nodes.push({id:'enter-cue',kind:'cue',position:{x:0,y:0},parameters:{id:1}});skill.edges.push({id:'enter',source:node.id,sourcePort:'enter',target:'enter-cue',targetPort:'execute',order:0});
  const before=structuredClone(skill),result=reconcileNodeOutputs(skill,skill.nodes.map(n=>n.id===node.id?{...n,parameters:{...n.parameters,persistent:false}}:n),node.id);
  assert.equal(result.nodes.find(n=>n.id==='enter-cue').enabled,false);assert(!result.edges.some(e=>e.id==='enter'));assert(result.edges.some(e=>e.source===node.id&&e.sourcePort==='apply'));assert.deepEqual(skill,before);
  const next=updateGraph(p,{kind:'skill',id:skill.id},()=>result);fs.writeFileSync(path.resolve(__dirname,'../../.local/verification/workbench-interactions/persistent-disabled.json'),JSON.stringify(next));
});
