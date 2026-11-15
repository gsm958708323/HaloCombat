const test=require('node:test');const assert=require('node:assert/strict');const fs=require('node:fs');const path=require('node:path');
const {extractProjectileBehavior,inlineBehaviorFields}=require('../../.local/web-checks/authoring/projectileBehavior.js');
const {connectGraph,copySelection,pasteSelection,deleteSelection}=require('../../.local/web-checks/authoring/graphEdits.js');
const {updateGraph,duplicateContent}=require('../../.local/web-checks/authoring/contentEdits.js');
const {extractProjectile}=require('../../.local/web-checks/authoring/projectileEdits.js');
const {mergeProjects}=require('../../.local/web-checks/authoring/mergeProjects.js');
const {skillSummary}=require('../../.local/web-checks/authoring/skillSummary.js');
const {nodeFieldGroups,nodePortGroups}=require('../../.local/web-checks/authoring/nodePresentation.js');
const {EditorHistory}=require('../../.local/web-checks/authoring/EditorHistory.js');
const example=()=>require('./projectFixture.cjs').readProject(path.resolve(__dirname,'../../../../Content/SkillLab/project.json'));
let sequence=0;const ids=()=>{let n=0;const namespace=++sequence;return ()=>'behavior-'+namespace+'-'+ ++n;};
const key={kind:'skill',id:'basic-shot'};
function extracted(source='basic-shot'){
  let p=example();const skill=p.skills.find(s=>s.id===source),root=skill.nodes.find(n=>n.kind==='projectile');
  p=updateGraph(p,{kind:'skill',id:source},g=>extractProjectileBehavior(g,root.id,ids()));return {p,g:p.skills.find(s=>s.id===source),root:root.id};
}

test('extracting piercing or chain preserves authored values in one undoable edit',()=>{
  for(const source of ['basic-shot','unlimited','chain-shot']){
    const id=source==='unlimited'?'basic-shot':source,p=example(),g=p.skills.find(s=>s.id===id);
    const root=g.nodes.find(n=>n.kind==='projectile');if(source==='unlimited')root.parameters.maxHits=0;
    const before=JSON.stringify(p),history=new EditorHistory(p);
    const next=updateGraph(p,{kind:'skill',id},graph=>extractProjectileBehavior(graph,root.id,ids()));history.edit(next);
    const result=history.current.skills.find(s=>s.id===id),b=result.nodes.find(n=>n.kind==='projectile-behavior'),body=result.nodes.find(n=>n.id===root.id);
    assert.equal(b.parameters.mode,root.parameters.chain?'chain':'pierce');assert(inlineBehaviorFields.every(f=>body.parameters[f]===undefined));
    if(root.parameters.chain){assert.equal(b.parameters.chainCount,root.parameters.chainCount);assert.equal(b.parameters.chainMode,root.parameters.chainMode);}
    else assert.equal(b.parameters.unlimited,root.parameters.maxHits===0);
    assert.equal(result.edges.filter(e=>e.target===root.id&&e.targetPort==='behavior').length,1);assert.equal(JSON.stringify(p),before);
    history.undo();assert.deepEqual(history.current,p);history.redo();assert.deepEqual(history.current,next);
  }
});

test('behavior connection replaces inline ownership and rejects effect wiring or a second input',()=>{
  const {g,root}=extracted(),b=g.nodes.find(n=>n.kind==='projectile-behavior');let graph={...g,edges:g.edges.filter(e=>e.targetPort!=='behavior')};
  graph.nodes=graph.nodes.map(n=>n.id===root?{...n,parameters:{...n.parameters,maxHits:9,chain:true}}:n);
  const result=connectGraph(graph,{source:b.id,sourceHandle:'behavior',target:root,targetHandle:'behavior'},'wire');
  assert.equal(result.nodes.find(n=>n.id===root).parameters.maxHits,undefined);assert.throws(()=>connectGraph(result,{source:b.id,sourceHandle:'behavior',target:root,targetHandle:'behavior'},'other'),/已有行为/);
  assert.throws(()=>connectGraph(graph,{source:b.id,sourceHandle:'behavior',target:'damage',targetHandle:'execute'}),/行为/);
  assert.throws(()=>connectGraph(graph,{source:'cast',sourceHandle:'cast',target:root,targetHandle:'behavior'}),/行为/);
});

test('deleting or disabling a behavior restores visible defaults without restoring removed inline fields',()=>{
  const {p,g,root}=extracted(),b=g.nodes.find(n=>n.kind==='projectile-behavior');
  const enabled=nodeFieldGroups(g.nodes.find(n=>n.id===root),{behaviorLinked:true});assert.equal(enabled.behaviors.length,0);
  const deleted=deleteSelection(p,key,[b.id],[]).skills.find(s=>s.id===key.id),body=deleted.nodes.find(n=>n.id===root);
  assert(!deleted.edges.some(e=>e.targetPort==='behavior'));assert.equal(body.parameters.maxHits,undefined);assert(nodeFieldGroups(body).behaviors.some(f=>f.key==='maxHits'));
  const disabled={...p,skills:p.skills.map(s=>s.id===g.id?{...s,nodes:s.nodes.map(n=>n.id===b.id?{...n,enabled:false}:n)}:s)};
  assert(!skillSummary(disabled,disabled.skills.find(s=>s.id===g.id)).items.some(i=>i.key.endsWith('/'+b.id)));
});

test('copy merge and duplication retain the data edge with fresh node and runtime identities',()=>{
  const {p,g,root}=extracted(),b=g.nodes.find(n=>n.kind==='projectile-behavior'),body=g.nodes.find(n=>n.id===root);
  const copied=pasteSelection(p,key,copySelection(g,[root,b.id]),{x:100,y:200},ids()).project.skills.find(s=>s.id===key.id);
  const pasted=copied.nodes.filter(n=>!g.nodes.some(v=>v.id===n.id));assert.equal(pasted.length,2);const newBody=pasted.find(n=>n.kind==='projectile');assert.notEqual(newBody.runtimeId,body.runtimeId);
  assert(copied.edges.some(e=>e.target===newBody.id&&e.targetPort==='behavior'&&pasted.some(n=>n.id===e.source)));
  const duplicated=duplicateContent(p,'skill',g.id,'copy',ids()),merged=mergeProjects(p,p,ids());
  for(const next of [duplicated.project.skills.find(s=>s.id===duplicated.id),merged.skills.slice(p.skills.length).find(s=>s.name===g.name)]){
    const edge=next.edges.find(e=>e.targetPort==='behavior');assert.equal(next.nodes.find(n=>n.id===edge.source).kind,'projectile-behavior');assert.equal(next.nodes.find(n=>n.id===edge.target).kind,'projectile');
  }
});

test('extracting a shared projectile moves its behavior and flight branches into one dependency graph',()=>{
  const {p,g,root}=extracted();const result=extractProjectile(p,key,root,ids()),definition=result.project.projectiles.at(-1),caller=result.project.skills.find(s=>s.id===g.id);
  assert(definition.nodes.some(n=>n.kind==='projectile-behavior'));assert(definition.edges.some(e=>e.targetPort==='behavior'));assert(!caller.nodes.some(n=>n.kind==='projectile-behavior'));
  assert.equal(caller.nodes.find(n=>n.id===root).kind,'projectile-ref');assert.equal(caller.edges.filter(e=>e.target===root).length,1);
  const output=path.resolve(__dirname,'../../.local/verification/projectile-policy');fs.mkdirSync(output,{recursive:true});fs.writeFileSync(path.join(output,'web-shared-behavior.json'),JSON.stringify(result.project));
});

test('a behavior shared outside the extracted branch cannot silently move away from its other consumer',()=>{
  const {p,g,root}=extracted(),b=g.nodes.find(n=>n.kind==='projectile-behavior');
  const next=updateGraph(p,key,s=>({...s,nodes:[...s.nodes,{...s.nodes.find(n=>n.id===root),id:'second'}],edges:[...s.edges,{id:'shared',source:b.id,sourcePort:'behavior',target:'second',targetPort:'behavior',order:1}]}));
  assert.throws(()=>extractProjectile(next,key,root,ids()),/其他弹体/);
});

test('flight modes expose only their active clock and keep a connected flight port visible',()=>{
  const {g,root}=extracted(),body=g.nodes.find(n=>n.id===root);
  for(const mode of ['none','time','distance']){
    const node={...body,parameters:{...body.parameters,flightTrigger:mode}},fields=nodeFieldGroups(node).main.map(f=>f.key);
    assert.equal(fields.includes('flightInterval'),mode==='time');assert.equal(fields.includes('flightDistance'),mode==='distance');
    assert.equal(nodePortGroups(node).main.some(([p])=>p==='flight'),mode!=='none');
  }
  assert(nodePortGroups({...body,parameters:{...body.parameters,flightTrigger:'none'}},{flight:1}).main.some(([p])=>p==='flight'));
});

test('effective summaries include behavior input once and retain flight branch labels',()=>{
  let {p,g,root}=extracted();const b=g.nodes.find(n=>n.kind==='projectile-behavior');
  p=updateGraph(p,key,s=>({...s,nodes:s.nodes.map(n=>n.id===root?{...n,parameters:{...n.parameters,flightTrigger:'time'}}:n),edges:s.edges.map(e=>e.source===root&&e.sourcePort==='hit'?{...e,sourcePort:'flight'}:e)}));g=p.skills.find(s=>s.id===g.id);
  const summary=skillSummary(p,g);assert.equal(summary.items.filter(i=>i.key.endsWith('/'+b.id)).length,1);assert(summary.items.some(i=>i.labels.includes('飞行触发')));
});
