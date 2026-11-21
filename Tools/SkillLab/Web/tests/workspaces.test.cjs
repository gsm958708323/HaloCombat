const test=require('node:test');
const assert=require('node:assert/strict');
const {EditorHistory}=require('../../.local/web-checks/authoring/EditorHistory.js');
const {prepareImport}=require('../../.local/web-checks/authoring/importCandidate.js');
const {ProjectSave,readDiskProject,rememberedWorkspace,rememberWorkspace}=require('../../.local/web-checks/authoring/projectIO.js');
const {planSkillCopy}=require('../../.local/web-checks/authoring/skillCopy.js');

const node=(id,kind,parameters={},extra={})=>({id,kind,parameters,position:{x:0,y:0},...extra});
const skill=(id,nodes)=>({id,name:id,runtimeId:1,nodes,edges:[]});
const project=(skills,extra={})=>({formatVersion:1,id:'project',name:'Project',skills,preview:{seed:1},...extra});
const motor={gravity:-20,jumpSpeed:6,airSteer:.35,groundY:0,stickDeadzone:.25};
const cue=(id,key)=>({id,key,kind:'cube',lifetime:.45});
const view=(id,resource,kind='body')=>({id,resource,kind,states:{},scale:1,yawOffset:0});
const runtime=(extra={})=>({motor,actors:[],combos:[],cues:[],...extra});
const candidate=project=>({project,detachedReferences:[]});
const byKind=(skill,kind)=>skill.nodes.find(node=>node.kind===kind);
const ids=()=>{let i=0;return ()=>`copied-${++i}`;};

test('disk loads resolve workspace, respect layout header, and bind save acknowledgements to each workspace',async t=>{
  const doc=project([skill('shot',[node('n','damage')])]);
  const requests=[];
  t.mock.method(globalThis,'fetch',async(url,options)=>{
    requests.push([url,options]);
    if(options.method==='POST')return new Response('{}',{headers:{ETag:`${options.headers['If-Match']}-updated`}});
    const arena=url.endsWith('arena');
    return new Response(JSON.stringify(doc),{headers:{ETag:arena?'arena-v1':'lab-v1','X-Workspace':arena?'arena':'lab','X-Needs-Layout':arena?'true':'false'}});
  });
  const lab=await readDiskProject();
  const arena=await readDiskProject('arena');
  assert.equal(lab.workspace,'lab');
  assert.equal(lab.project.skills[0].nodes[0].position.y,0);
  assert.equal(arena.workspace,'arena');
  assert.notEqual(arena.project.skills[0].nodes[0].position.y,0);
  const arenaSave=new ProjectSave(arena.project,arena.etag,arena.workspace);
  const labSave=new ProjectSave(lab.project,lab.etag,lab.workspace);
  await arenaSave.save({...arena.project,name:'Arena edit'});
  await labSave.save({...lab.project,name:'Lab edit'});
  await arenaSave.save({...arena.project,name:'Second Arena edit'});
  assert.deepEqual(requests.map(([url,options])=>[url,options.headers?.['If-Match']]),[
    ['/api/project',undefined],['/api/project?workspace=arena',undefined],['/api/project?workspace=arena','arena-v1'],
    ['/api/project?workspace=lab','lab-v1'],['/api/project?workspace=arena','arena-v1-updated']
  ]);
  await assert.rejects(readDiskProject('invalid'),/工作区或版本标记/);
});

test('workspace preference ignores invalid and inaccessible browser storage',()=>{
  const original=Object.getOwnPropertyDescriptor(globalThis,'localStorage');
  let value='invalid';
  try {
    Object.defineProperty(globalThis,'localStorage',{configurable:true,value:{getItem:()=>value,setItem:(_,workspace)=>{value=workspace;}}});
    assert.equal(rememberedWorkspace(),undefined);
    rememberWorkspace('arena');
    assert.equal(rememberedWorkspace(),'arena');
    Object.defineProperty(globalThis,'localStorage',{configurable:true,get:()=>{throw new Error('disabled');}});
    assert.equal(rememberedWorkspace(),undefined);
    assert.doesNotThrow(()=>rememberWorkspace('lab'));
  } finally {
    if(original)Object.defineProperty(globalThis,'localStorage',original);
    else delete globalThis.localStorage;
  }
});

test('copy previews conflicting content and remaps nested references in one undoable change',()=>{
  const source=project([
    skill('root',[node('cast','cast',{recastSkill:'child'}),node('buff','buff',{buff:'burn'})]),
    skill('child',[])
  ],{buffs:[{id:'burn',name:'Burn',runtimeId:8000,nodes:[],edges:[]}],editor:{selectedBuff:'burn',comboViews:[{id:'ignore'}]}});
  const target=project([skill('root',[]),skill('child',[])],{buffs:[{id:'burn',runtimeId:8000,nodes:[],edges:[]}],editor:{selectedSkill:'child',comboViews:[]}});
  target.skills[1].runtimeId=2;
  const plan=planSkillCopy(target,candidate(source),'root',ids());
  assert.equal(plan.dependencies.filter(dependency=>dependency.resolution==='rename').length,3);
  const copied=plan.project.skills.find(s=>s.id===plan.root);
  assert.notEqual(copied.id,'root');
  assert.equal(byKind(copied,'cast').parameters.recastSkill,plan.project.skills.at(-1).id);
  assert.equal(byKind(copied,'buff').parameters.buff,plan.project.buffs.at(-1).id);
  assert.notEqual(byKind(copied,'cast').id,'cast');
  assert.equal(new Set(plan.project.skills.map(s=>s.runtimeId)).size,4);
  assert.deepEqual(plan.project.editor.comboViews,target.editor.comboViews);
  assert.equal(plan.project.editor.selectedSkill,plan.root);
  assert.equal(plan.project.preview,target.preview);
  const history=new EditorHistory(target);
  history.edit(plan.project);
  assert.equal(history.undo(),true);
  assert.deepEqual(history.current,target);
  assert.equal(history.canUndo,false);
  assert.equal(history.redo(),true);
  assert.deepEqual(history.current,plan.project);
});

test('copy remaps Cue collisions and body Views while preserving target Arena, actors, combos and Motor',()=>{
  const targetRuntime=runtime({actors:[{id:'target-actor'}],arena:{player:'target-actor'},combos:[{id:'target-combo'}],
    cues:[cue(1,'old'),cue(3,'shared')],views:[view('body','target')]});
  const target=project([],{runtime:targetRuntime});
  const source=project([skill('root',[
    node('implicit','cue'),node('pulse','delivery',{cueId:1,view:'body'}),node('shot','projectile',{cueId:2}),node('shared','cue',{id:3}),
    node('barrel','construct',{view:'barrel'})
  ])],{runtime:runtime({motor:{...motor,gravity:-33},arena:{player:'source-actor'},actors:[{id:'source-actor'}],combos:[{id:'source-combo'}],
    views:[view('body','source'),view('barrel','a built-in name'),view('actor','source-actor','actor')],cues:[cue(1,'new'),cue(2,'free'),cue(3,'shared'),cue(4,'unused')]})});
  const plan=planSkillCopy(target,candidate(source),'root',ids());
  const result=plan.project.runtime;
  for(const field of ['motor','arena','actors','combos'])assert.equal(result[field],targetRuntime[field]);
  assert.equal(plan.motorDiffers,true);
  assert.equal(result.views.length,2);
  assert.equal(result.cues.length,4);
  assert.equal(result.cues.find(cue=>cue.id===1).key,'old');
  assert.equal(result.cues.find(cue=>cue.id===2).key,'free');
  const mapped=result.cues.find(cue=>cue.key==='new').id;
  const root=plan.project.skills[0];
  assert.equal(byKind(root,'cue').parameters.id,mapped);
  assert.equal(byKind(root,'delivery').parameters.cueId,mapped);
  assert.equal(byKind(root,'projectile').parameters.cueId,2);
  assert.equal(byKind(root,'delivery').parameters.view,result.views[1].id);
  assert.equal(byKind(root,'construct').parameters.view,'barrel');
  assert.equal(plan.dependencies.find(dependency=>dependency.kind==='cues'&&dependency.from==='3').resolution,'reuse');
  assert.equal(source.skills[0].nodes[0].parameters.id,undefined);
});

test('reported and omitted dormant references stay detached even when their IDs exist at the target',()=>{
  const detached={path:'$.skills[0].nodes[dormant].parameters.buff',nodeId:'dormant',field:'buff',originalId:'old'};
  const raw=project([skill('root',[
    node('dormant','buff',{buff:'old'},{enabled:false}),node('reference','area-ref',{area:'outside'},{enabled:false}),
    node('graphic','projectile',{view:'body'},{enabled:false}),node('effect','cue',{id:1},{enabled:false})
  ])],{runtime:runtime()});
  const target=project([],{buffs:[{id:'old',runtimeId:8000,nodes:[],edges:[]}],areas:[{id:'outside',nodes:[],edges:[]}],
    runtime:runtime({views:[view('body','target')],cues:[cue(1,'target')]})});
  const imported=prepareImport({json:JSON.stringify(raw),needsLayout:true,detachedReferences:[detached]});
  const plan=planSkillCopy(target,imported,'root',ids());
  const root=plan.project.skills[0];
  assert.equal(plan.detachedReferences.length,4);
  assert.deepEqual(plan.detachedReferences[0],detached);
  assert.equal(byKind(root,'buff').parameters.buff,undefined);
  assert.equal(byKind(root,'area-ref').parameters.area,undefined);
  assert.equal(byKind(root,'projectile').parameters.view,undefined);
  assert.equal(byKind(root,'cue').parameters.id,undefined);
  assert.equal(byKind(root,'cue').enabled,false);
  assert.notEqual(byKind(root,'buff').position.y,0);
});

test('copy into an implicit-runtime Lab retains default Motor and supplies target fallback Cues',()=>{
  const target=project([skill('existing',[node('existing-cue','cue',{id:8})])]);
  const source=project([skill('root',[node('body','projectile',{cueId:8,view:'body'})])],{
    runtime:runtime({motor:{...motor,gravity:-32},cues:[cue(8,'source')],views:[view('body','resource')]})
  });
  const plan=planSkillCopy(target,candidate(source),'root',ids());
  assert.deepEqual(plan.project.runtime.motor,motor);
  assert.equal(plan.motorDiffers,true);
  assert.deepEqual(plan.project.runtime.actors,[]);
  assert.deepEqual(plan.project.runtime.combos,[]);
  assert.equal(plan.project.runtime.cues.find(cue=>cue.id===8).key,'cue-8');
  const incoming=plan.project.runtime.cues.find(cue=>cue.key==='source');
  assert.notEqual(incoming.id,8);
  assert.equal(plan.project.skills.at(-1).nodes[0].parameters.cueId,incoming.id);
});

test('Arena publish saves the captured draft, then sends only the new If-Match token',async t=>{
  const arena=project([]),store=new ProjectSave(arena,'arena-v1','arena');
  const draft={...arena,name:'Ready'};
  const requests=[];
  t.mock.method(globalThis,'fetch',async(url,options)=>{
    requests.push([url,options]);
    if(url.startsWith('/api/project'))return new Response('{}',{headers:{ETag:'arena-v2'}});
    return Response.json({message:'published'});
  });
  assert.equal((await store.publishArena(draft)).message,'published');
  assert.equal(requests.length,2);
  assert.equal(requests[0][1].headers['If-Match'],'arena-v1');
  assert.deepEqual(JSON.parse(requests[0][1].body),draft);
  assert.deepEqual(requests[1],['/api/runtime/publish?workspace=arena',{method:'POST',headers:{'If-Match':'arena-v2'}}]);
  assert.equal(store.dirty({...draft,name:'Later edit'}),true);
  assert.equal(store.dirty(draft),false);
  await store.publishArena(draft);
  assert.deepEqual(requests[2],requests[1]);
});

test('stale Arena drafts preserve edits on save conflict and clean tabs still check If-Match',async t=>{
  const arena=project([]),store=new ProjectSave(arena,'stale','arena');
  const draft={...arena,name:'Unsaved work'};
  const calls=[];
  t.mock.method(globalThis,'fetch',async(url,options)=>{
    calls.push([url,options.headers['If-Match']]);
    return Response.json({error:'stale tab'},{status:409});
  });
  await assert.rejects(store.publishArena(draft),/stale tab/);
  assert.equal(store.dirty(draft),true);
  assert.equal(store.busy,false);
  await assert.rejects(store.publishArena(arena),/stale tab/);
  assert.deepEqual(calls,[['/api/project?workspace=arena','stale'],['/api/runtime/publish?workspace=arena','stale']]);
  await assert.rejects(new ProjectSave(arena,'lab-v1','lab').publishArena(arena),/只有 Arena/);
});

test('copy remaps all uses of colliding projectile tracking keys across the closure',()=>{
  const source=project([skill('root',[
    node('cast','cast',{trackingKey:'warp'}),node('teleport','teleport-projectile',{trackingKey:'warp'}),
    node('other','projectile',{trackingKey:'warp-copy'})
  ])],{projectiles:[{id:'tracked',root:'launch',nodes:[node('launch','projectile',{trackingKey:'warp'})],edges:[]}]});
  const target=project([skill('existing',[node('shot','projectile',{trackingKey:'warp'})])]);
  const plan=planSkillCopy(target,candidate(source),'root',ids());
  assert.deepEqual(plan.trackingKeys,[{from:'warp',to:'warp-copy-2'}]);
  const copied=plan.project.skills.find(skill=>skill.id===plan.root);
  assert.equal(byKind(copied,'cast').parameters.trackingKey,'warp-copy-2');
  assert.equal(byKind(copied,'teleport-projectile').parameters.trackingKey,'warp-copy-2');
  assert.equal(plan.project.projectiles[0].nodes[0].parameters.trackingKey,'warp-copy-2');
  assert.equal(byKind(copied,'projectile').parameters.trackingKey,'warp-copy');
  assert.equal(plan.project.skills[0].nodes[0].parameters.trackingKey,'warp');
  assert.equal(source.projectiles[0].nodes[0].parameters.trackingKey,'warp');
});
