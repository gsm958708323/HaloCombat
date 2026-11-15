const test=require('node:test');const assert=require('node:assert/strict');
const fs=require('node:fs');const path=require('node:path');
const edits=require('../../.local/web-checks/authoring/contentEdits.js');
const graph=require('../../.local/web-checks/authoring/graphEdits.js');
const {mergeProjects}=require('../../.local/web-checks/authoring/mergeProjects.js');
const {EditorHistory}=require('../../.local/web-checks/authoring/EditorHistory.js');
const {ProjectSave}=require('../../.local/web-checks/authoring/projectIO.js');
const example=()=>JSON.parse(fs.readFileSync(path.resolve(__dirname,'../../../../Content/SkillLab/project.json'),'utf8'));
const ids=()=>{let number=0;return ()=> 'generated-'+ ++number;};
const skillKey={kind:'skill',id:'basic-shot'};
const runtime=()=>JSON.parse(fs.readFileSync(path.resolve(__dirname,'../../../../Assets/StreamingAssets/Combat/project.json'),'utf8'));
const areaEdits=require('../../.local/web-checks/authoring/areaEdits.js');

test('extracting an inline persistent area moves its lifecycle graph and retains placement at the caller',()=>{
  const p=example(),s=p.skills.find(s=>s.nodes.some(n=>n.kind==='delivery'&&n.parameters.mode==='area'&&n.parameters.persistent===true));
  const root=s.nodes.find(n=>n.kind==='delivery'&&n.parameters.mode==='area'&&n.parameters.persistent===true),before=structuredClone(p),history=new EditorHistory(p);
  const result=areaEdits.extractArea(p,{kind:'skill',id:s.id},root.id,ids());history.edit(result.project);
  const definition=result.project.areas.at(-1),reference=result.project.skills.find(v=>v.id===s.id).nodes.find(n=>n.id===root.id);
  assert.equal(definition.nodes.find(n=>n.id===definition.root).runtimeId,root.runtimeId);assert.equal(reference.kind,'area-ref');assert.equal(reference.parameters.area,definition.id);
  assert.equal(definition.nodes.find(n=>n.id===definition.root).parameters.center,undefined);assert.equal(result.project.editor.selectedArea,definition.id);history.undo();assert.deepEqual(history.current,before);
});
test('shared area rename replacement deletion and duplication keep typed references and fresh root identity',()=>{
  const p=runtime(),id='arena-bomb-explosion',renamed=edits.renameContent(p,'area',id,'shared-blast');assert.equal(edits.referencesTo(renamed,'area','shared-blast').length,3);
  assert.throws(()=>edits.deleteContent(renamed,'area','shared-blast'),/替代引用/);const copied=edits.duplicateContent(renamed,'area','shared-blast','copy',ids());
  const old=renamed.areas.find(a=>a.id==='shared-blast'),next=copied.project.areas.at(-1);assert.notEqual(next.root,old.root);assert.notEqual(next.nodes.find(n=>n.id===next.root).runtimeId,old.nodes.find(n=>n.id===old.root).runtimeId);
  const deleted=edits.deleteContent(copied.project,'area','shared-blast',copied.id);assert.equal(edits.referencesTo(deleted,'area',copied.id).length,3);
});
test('merging a bomb rebinds projectile and delayed area references to the imported area definitions',()=>{
  const p=runtime(),merged=mergeProjects(p,p,ids()),areas=merged.areas.slice(p.areas.length),allowed=new Set(areas.map(a=>a.id));
  const imported=[...areas,...merged.projectiles.slice(p.projectiles.length)];assert(imported.flatMap(a=>a.nodes).filter(n=>n.kind==='area-ref').every(n=>allowed.has(n.parameters.area)));
  assert.equal(new Set(merged.areas.map(a=>a.nodes.find(n=>n.id===a.root).runtimeId)).size,merged.areas.length);
});
test('area roots resist deletion and imported graph roots cannot be pasted into their effect branches',()=>{
  const p=runtime(),a=p.areas[0],key={kind:'area',id:a.id};assert(graph.deleteSelection(p,key,[a.root],[]).areas[0].nodes.some(n=>n.id===a.root));
  const source=p.skills[0],clipboard=graph.copySelection(source,source.nodes.filter(n=>['cast','target'].includes(n.kind)).map(n=>n.id));assert.throws(()=>graph.pasteSelection(p,key,clipboard,{x:100,y:100},ids()),/根节点/);
});
test('area viewport rename and new definitions preserve existing graph selection metadata',()=>{
  let p=runtime();p=edits.selectGraph(p,'arena-bomb-shot','','','','arena-bomb-explosion');p=edits.saveViewport(p,{kind:'area',id:'arena-bomb-explosion'},{x:20,y:30,zoom:.6});
  p=edits.renameContent(p,'area','arena-bomb-explosion','blast');assert.equal(p.editor.selectedArea,'blast');assert.equal(p.editor.viewports.at(-1).id,'blast');
  const result=areaEdits.newArea(p,'arena-bomb-shot',ids());assert.equal(result.project.editor.selectedArea,result.id);assert.equal(result.project.areas.at(-1).nodes[0].parameters.persistent,true);
});

test('copying and pasting a construct preserve lifecycle branches with fresh definition identities',()=>{
  const p=example(),source=p.skills.find(s=>s.id==='barrel-chain'),key={kind:'skill',id:source.id};
  const original=source.nodes.find(n=>n.kind==='construct');const copy=edits.duplicateContent(p,'skill',source.id,'copy',ids()).project.skills.at(-1);
  const body=copy.nodes.find(n=>n.kind==='construct');assert.notEqual(body.runtimeId,original.runtimeId);assert(copy.edges.some(e=>e.source===body.id&&e.sourcePort==='death'));
  const pasted=graph.pasteSelection(p,key,graph.copySelection(source,[original.id]),{x:100,y:100},ids());
  const second=pasted.project.skills.find(s=>s.id===key.id).nodes.at(-1);assert.notEqual(second.runtimeId,original.runtimeId);assert.equal(second.parameters.hp,5);
});

test('skill rename and delete update ammunition fallback and projectile recast references',()=>{
  const p=example();assert(edits.referencesTo(p,'skill','reload-ammo').some(r=>r.field==='fallbackSkill'));
  let next=edits.renameContent(p,'skill','reload-ammo','magazine-reload');assert.equal(next.skills.find(s=>s.id==='ammo-shot').nodes.find(n=>n.kind==='cast').parameters.fallbackSkill,'magazine-reload');
  assert.throws(()=>edits.deleteContent(next,'skill','magazine-reload'),/替代引用/);
  next=edits.deleteContent(next,'skill','projectile-warp','magazine-reload');assert.equal(next.skills.find(s=>s.id==='tracked-shot').nodes.find(n=>n.kind==='cast').parameters.recastSkill,'magazine-reload');
  assert.equal(p.skills.find(s=>s.id==='ammo-shot').nodes.find(n=>n.kind==='cast').parameters.fallbackSkill,'reload-ammo');
});
test('merge binds imported ammunition and recast routes to their own imported skill identities',()=>{
  const p=example(),next=mergeProjects(p,p,ids()),imported=next.skills.slice(p.skills.length);
  const reload=imported.find(s=>s.name===p.skills.find(s=>s.id==='reload-ammo').name),warp=imported.find(s=>s.name===p.skills.find(s=>s.id==='projectile-warp').name);
  assert.equal(imported.find(s=>s.name===p.skills.find(s=>s.id==='ammo-shot').name).nodes.find(n=>n.kind==='cast').parameters.fallbackSkill,reload.id);
  assert.equal(imported.find(s=>s.name===p.skills.find(s=>s.id==='tracked-shot').name).nodes.find(n=>n.kind==='cast').parameters.recastSkill,warp.id);
});
test('pasting a cast cannot silently retain a missing fallback skill from another project',()=>{
  const p=example(),cast=p.skills.find(s=>s.id==='ammo-shot').nodes.find(n=>n.kind==='cast');
  p.skills=[{...p.skills[0],nodes:[],edges:[]}];assert.throws(()=>graph.pasteSelection(p,skillKey,{nodes:[cast],edges:[]},{x:0,y:0},ids()),/不存在的技能/);
});

test('runtime skill rename updates Combo predecessor/target, dodge, actor AI and loadout slots',()=>{
  const p=runtime();const next=edits.renameContent(p,'skill','g1','new-g1');
  assert.equal(next.runtime.combos[0].toSkill,'new-g1');assert(next.runtime.combos[1].preSkills.includes('new-g1'));
  assert(next.runtime.actors.filter(a=>a.skillId).some(a=>a.skillId==='new-g1'));assert.equal(next.runtime.actors[0].slots[0],'new-g1');
  assert.equal(edits.referencesTo(next,'skill','g1').length,0);assert.throws(()=>edits.deleteContent(p,'skill','g1'),/替代引用/);
});
test('authored timeline copy remaps every entry to the new nodes and allocates a fresh timeline ID',()=>{
  const p=runtime(),source=p.skills[0];const result=edits.duplicateContent(p,'skill',source.id,'timeline-copy',ids());const copied=result.project.skills.at(-1);
  assert(!p.skills.some(s=>s.runtimeId===copied.runtimeId||s.timeline?.runtimeId===copied.timeline.runtimeId));
  const valid=new Set(copied.nodes.map(n=>n.id));for(const row of [...copied.timeline.clips,...copied.timeline.payloads])assert((row.effects??[]).every(id=>valid.has(id)));
  assert.deepEqual(copied.timeline.clips.map(c=>[c.start,c.end,c.kind]),source.timeline.clips.map(c=>[c.start,c.end,c.kind]));
});
test('Timeline controls and speed scaling survive copy and merge without sharing mutable tag arrays',()=>{
  const p=runtime(),source=p.skills.find(s=>s.id==='arena-controlled-shot');assert(source.timeline.scaleWithActionSpeed);
  const next=edits.duplicateContent(p,'skill',source.id,'copy',ids()).project;const copied=next.skills.at(-1);
  assert.deepEqual(copied.timeline.controlTags,[1011,1012,1013]);copied.timeline.controlTags.pop();assert.deepEqual(source.timeline.controlTags,[1011,1012,1013]);
  const imported=mergeProjects(p,p,ids()).skills.slice(p.skills.length).find(s=>s.name===source.name);
  assert.deepEqual(imported.timeline.controlTags,source.timeline.controlTags);assert.equal(imported.timeline.scaleWithActionSpeed,true);
});
test('deleting an authored effect node removes its timeline entry and undo restores both',()=>{
  const p=runtime(),key={kind:'skill',id:'g1'},history=new EditorHistory(p);
  history.edit(graph.deleteSelection(p,key,['slash-cue'],[]));assert(!history.current.skills[0].timeline.payloads[0].effects.length);
  history.undo();assert.deepEqual(history.current.skills[0].timeline.payloads[0].effects,['slash-cue']);
});
test('merging runtime content preserves authored timing and rewrites all imported Combo and actor references',()=>{
  const p=runtime();const next=mergeProjects(p,p,ids());const imported=next.skills.slice(p.skills.length);const valid=new Set(imported.map(s=>s.id));
  assert(next.runtime.combos.slice(p.runtime.combos.length).every(c=>valid.has(c.toSkill)&&c.preSkills.every(id=>valid.has(id))));
  assert(next.runtime.actors.slice(p.runtime.actors.length).every(a=>(!a.skillId||valid.has(a.skillId))&&(!a.dodgeSkill||valid.has(a.dodgeSkill))&&(a.slots??[]).every(id=>valid.has(id))));
  for(const s of imported){const owner=s.timelineRef?next.timelines.find(t=>t.id===s.timelineRef):s;assert(owner);assert([...owner.timeline.clips,...owner.timeline.payloads].every(c=>(c.effects??[]).every(id=>owner.nodes.some(n=>n.id===id))));}
  assert.deepEqual(next.runtime.arena,p.runtime.arena);
});
test('imported Arena remaps actor and input references without selecting an existing Combo root',()=>{
  const source=runtime(),destination=runtime();delete destination.runtime.arena;
  const next=mergeProjects(destination,source,ids()),arena=next.runtime.arena;
  const importedActors=next.runtime.actors.slice(destination.runtime.actors.length),importedCombos=next.runtime.combos.slice(destination.runtime.combos.length);
  for(const role of ['player','enemy','trainingTarget'])assert(importedActors.some(a=>a.id===arena[role]));
  for(const binding of arena.bindings){assert(importedCombos.some(c=>c.input===binding.input&&c.preSkills.length===0));assert(!destination.runtime.combos.some(c=>c.input===binding.input));}
  assert.notEqual(arena.player,source.runtime.arena.player);assert.equal(source.runtime.arena.bindings[0].input,'ArenaPrimary');
});

test('skill ID rename updates blueprint references and editor selection, preserving numeric identity',()=>{
  const p=example();p.editor={selectedSkill:'basic-shot',viewports:[{...skillKey,x:20,y:30,zoom:.5}]};
  const next=edits.renameContent(p,'skill','basic-shot','renamed');
  assert.equal(next.skills[0].runtimeId,p.skills[0].runtimeId);assert.equal(next.summonBlueprints[0].skillId,'renamed');
  assert.equal(next.editor.selectedSkill,'renamed');assert.equal(next.editor.viewports[0].id,'renamed');assert.equal(p.skills[0].id,'basic-shot');
});
test('Buff rename rewrites disabled and retained fields in every graph, leaving arbitrary cue text alone',()=>{
  const p=example();p.skills[0].nodes.push({id:'disabled',kind:'dispel',enabled:false,position:{x:0,y:0},parameters:{mode:'source',buff:'burn'}});
  p.buffs[1].nodes.push({id:'nested',kind:'buff',position:{x:0,y:0},parameters:{buff:'burn'}});
  p.skills[0].nodes.push({id:'text',kind:'cue',position:{x:0,y:0},parameters:{name:'burn'}});
  const before=edits.referencesTo(p,'buff','burn');assert(before.some(r=>r.nodeId==='disabled'));assert(before.some(r=>r.nodeId==='nested'));
  const next=edits.renameContent(p,'buff','burn','fire-status');assert.equal(edits.referencesTo(next,'buff','burn').length,0);
  assert.equal(edits.referencesTo(next,'buff','fire-status').length,before.length);assert.equal(next.skills[0].nodes.at(-1).parameters.name,'burn');
});
test('blueprint rename reaches Buff graph and ID conflicts are rejected without mutation',()=>{
  const p=example();p.buffs[0].nodes.push({id:'spawn',kind:'delivery',position:{x:0,y:0},parameters:{mode:'summon',blueprint:'echo-gunner'}});
  const next=edits.renameContent(p,'blueprint','echo-gunner','minion');assert.equal(next.buffs[0].nodes.at(-1).parameters.blueprint,'minion');
  assert.throws(()=>edits.renameContent(p,'buff','burn','battle-rage'),/已存在/);assert.throws(()=>edits.renameContent(p,'skill','basic-shot','  '),/不能为空/);
  assert.equal(p.summonBlueprints[0].id,'echo-gunner');
});
test('copy allocates graph, edge, skill and definition identities while keeping shared references',()=>{
  const p=example(),before=JSON.stringify(p);const source=p.skills.find(s=>s.id==='burn-shot');assert(source);
  const result=edits.duplicateContent(p,'skill',source.id,'copy',ids());const copy=result.project.skills.at(-1);
  assert.equal(copy.name,'copy');assert.notEqual(copy.runtimeId,source.runtimeId);assert(!source.nodes.some(n=>copy.nodes.some(c=>c.id===n.id)));
  assert(!source.edges.some(e=>copy.edges.some(c=>c.id===e.id)));assert.equal(copy.nodes.find(n=>n.kind==='buff').parameters.buff,'burn');
  const oldNumbers=new Set(edits.allGraphs(p).flatMap(({graph})=>graph.nodes.map(n=>n.runtimeId).filter(Boolean)));
  for(const n of copy.nodes.filter(n=>n.kind==='projectile'||n.kind==='delivery'))assert(!oldNumbers.has(n.runtimeId));
  copy.nodes[0].parameters.range=999;assert.equal(JSON.stringify(p),before);
});
test('delete rejects live references and atomically replaces all incoming uses',()=>{
  const p=example();assert.throws(()=>edits.deleteContent(p,'buff','burn'),/替代引用/);
  const next=edits.deleteContent(p,'buff','burn','battle-rage');assert(!next.buffs.some(b=>b.id==='burn'));
  assert.equal(edits.referencesTo(next,'buff','burn').length,0);assert(edits.referencesTo(next,'buff','battle-rage').length>0);assert(p.buffs.some(b=>b.id==='burn'));
});
test('deleting the last skill yields a persistable empty authoring project and removes its view',()=>{
  const p=example();p.skills=[p.skills[0]];p.buffs=[];p.summonBlueprints=[];p.editor={selectedSkill:p.skills[0].id,viewports:[{...skillKey,x:0,y:0,zoom:1}]};
  const next=edits.deleteContent(p,'skill','basic-shot');assert.equal(next.skills.length,0);assert.equal(next.editor.selectedSkill,'');assert.equal(next.editor.viewports.length,0);
});
test('clipboard keeps only internal edges and paste remaps endpoints and definition identities',()=>{
  const p=example();const g=p.skills[0];const selected=g.nodes.filter(n=>['projectile','damage'].includes(n.kind));
  const clipboard=graph.copySelection(g,selected.map(n=>n.id));assert.equal(clipboard.edges.length,1);
  const result=graph.pasteSelection(p,skillKey,clipboard,{x:100,y:50},ids());const pasted=result.project.skills[0].nodes.filter(n=>result.ids.includes(n.id));
  assert.equal(pasted.length,2);assert.equal(Math.min(...pasted.map(n=>n.position.x)),100);assert.equal(Math.min(...pasted.map(n=>n.position.y)),50);
  const copiedEdge=result.project.skills[0].edges.at(-1);assert(result.ids.includes(copiedEdge.source)&&result.ids.includes(copiedEdge.target));
  assert.equal(copiedEdge.order,clipboard.edges[0].order);assert.equal(g.nodes.length,p.skills[0].nodes.length);
});
test('cross-graph paste skips incompatible roots and keeps the Buff lifecycle root unique',()=>{
  const p=example();const clip=graph.copySelection(p.skills[0],p.skills[0].nodes.map(n=>n.id));
  const result=graph.pasteSelection(p,{kind:'buff',id:'burn'},clip,{x:50,y:50},ids());assert.equal(result.skipped,2);
  const buff=result.project.buffs[0];assert.equal(buff.nodes.filter(n=>n.kind==='buff-events').length,1);assert(!buff.nodes.some(n=>n.kind==='cast'||n.kind==='target'));
});
test('multi-delete is one graph transform and protects the Buff lifecycle root',()=>{
  const p=example(),key={kind:'buff',id:'burn'},buff=p.buffs[0];
  const next=graph.deleteSelection(p,key,buff.nodes.map(n=>n.id),buff.edges.map(e=>e.id));assert.equal(next.buffs[0].nodes.length,1);assert.equal(next.buffs[0].nodes[0].kind,'buff-events');assert.equal(next.buffs[0].edges.length,0);
});
test('pasting a draft with duplicate root kinds keeps only one root of each kind',()=>{
  const p=example();const target=p.skills[0].nodes[0];p.skills[0].nodes=[];p.skills[0].edges=[];
  const pasted=graph.pasteSelection(p,skillKey,{nodes:[target,{...structuredClone(target),id:'other-target'}],edges:[]},{x:0,y:0},ids());
  assert.equal(pasted.ids.length,1);assert.equal(pasted.skipped,1);
});
test('connection checks data types, occupied target input and directed cycles',()=>{
  const g=example().skills[0];assert.throws(()=>graph.connectGraph(g,{source:'damage',sourceHandle:'next',target:'cast',targetHandle:'target'},'bad'),/匹配|目标/);
  assert.throws(()=>graph.connectGraph(g,{source:'target',sourceHandle:'target',target:'cast',targetHandle:'target'},'duplicate'),/已有目标/);
  assert.throws(()=>graph.connectGraph(g,{source:'damage',sourceHandle:'next',target:'projectile',targetHandle:'execute'},'cycle'),/循环/);
});
test('repeated execution edges preserve distinct IDs and order, and reconnect preserves identity',()=>{
  const g=example().skills[0];const next=graph.connectGraph(g,{source:'projectile',sourceHandle:'hit',target:'damage',targetHandle:'execute'},'repeat');
  const siblings=next.edges.filter(e=>e.source==='projectile'&&e.sourcePort==='hit');assert.equal(siblings.length,2);assert.notEqual(siblings[0].order,siblings[1].order);
  const reordered=graph.reorderEdge(next,'repeat',-1);assert.equal(reordered.edges.find(e=>e.id==='repeat').order,0);
  const reconnected=graph.connectGraph(next,{source:'projectile',sourceHandle:'expire',target:'damage',targetHandle:'execute'},'repeat',true);assert.equal(reconnected.edges.length,next.edges.length);assert.equal(reconnected.edges.at(-1).id,'repeat');
});
test('reconnecting the target data edge excludes its old endpoint from occupancy checks',()=>{
  const g=example().skills[0],targetEdge=g.edges[0];const result=graph.connectGraph(g,{source:targetEdge.source,sourceHandle:targetEdge.sourcePort,target:targetEdge.target,targetHandle:targetEdge.targetPort},targetEdge.id,true);
  assert.equal(result.edges.length,g.edges.length);assert.equal(result.edges.at(-1).id,targetEdge.id);assert.equal(result.edges.at(-1).order,targetEdge.order);
});
test('merge resolves colliding IDs within imported references without binding them to old shared definitions',()=>{
  const source=example(),destination=example(),before=JSON.stringify(destination);const next=mergeProjects(destination,source,ids());
  assert.equal(next.skills.length,source.skills.length*2);assert.equal(next.buffs.length,source.buffs.length*2);
  const importedSkills=next.skills.slice(destination.skills.length),importedBuffs=next.buffs.slice(destination.buffs.length),importedBlueprints=next.summonBlueprints.slice(destination.summonBlueprints.length);
  assert(importedSkills.every(s=>!destination.skills.some(d=>d.id===s.id)));assert.equal(new Set(next.skills.map(s=>s.runtimeId)).size,next.skills.length);
  const importedBurn=importedBuffs.find(b=>b.name==='灼烧');assert(importedSkills.some(s=>s.nodes.some(n=>n.kind==='buff'&&n.parameters.buff===importedBurn.id)));
  assert(importedBlueprints.every(b=>importedSkills.some(s=>s.id===b.skillId)));assert.equal(JSON.stringify(destination),before);
});
test('one drag with 120 positions occupies one undo transaction and redo restores the final state',()=>{
  const p=example(),history=new EditorHistory(p);for(let i=1;i<=120;i++)history.edit({...history.current,name:'position-'+i},'drag');
  assert(history.undo());assert.equal(history.current.name,p.name);assert(!history.canUndo);assert(history.redo());assert.equal(history.current.name,'position-120');
});
test('new edit after undo clears redo; metadata changes do not consume undo or clear redo',()=>{
  const p=example(),history=new EditorHistory(p);history.edit({...p,name:'edited'});history.undo();history.edit(edits.selectGraph(history.current,'burst-shot'),'',false);
  assert(history.canRedo);history.redo();assert.equal(history.current.name,'edited');history.undo();history.edit({...history.current,name:'different'});assert(!history.canRedo);
});
test('content delete and rename undo restore all references and graph selection',()=>{
  const p=example(),history=new EditorHistory(edits.selectGraph(p,'basic-shot','burn'));history.edit(edits.renameContent(history.current,'buff','burn','fire'));
  history.edit(edits.deleteContent(history.current,'buff','fire','battle-rage'));history.undo();assert(history.current.buffs.some(b=>b.id==='fire'));history.undo();assert.equal(history.current.editor.selectedBuff,'burn');assert(edits.referencesTo(history.current,'buff','burn').length>0);
});
test('save acknowledges its immutable request snapshot and leaves subsequent edits dirty',async()=>{
  const p=example(),saving=new ProjectSave(p,'old');let complete;let sent;
  const request=saving.save({...p,name:'sent'},(snapshot,etag)=>{assert.equal(etag,'old');sent=snapshot;return new Promise(resolve=>complete=resolve);});
  assert(saving.saving);const latest={...p,name:'newer'};complete('new');await request;
  assert.equal(sent.name,'sent');assert(saving.dirty(latest));assert(!saving.dirty({...p,name:'sent'}));assert(!saving.saving);
});
test('concurrent saves are refused and failed writes retain the previous baseline and ETag',async()=>{
  const p=example(),saving=new ProjectSave(p,'disk');let reject;
  const request=saving.save({...p,name:'unsaved'},()=>new Promise((_,failure)=>reject=failure));await assert.rejects(()=>saving.save(p),/正在保存/);
  reject(new Error('conflict'));await assert.rejects(()=>request,/conflict/);assert(saving.failure.includes('conflict'));assert(saving.dirty({...p,name:'unsaved'}));
  await saving.save({...p,name:'retry'},async(_,etag)=>{assert.equal(etag,'disk');return 'next';});assert.equal(saving.failure,'');assert(!saving.dirty({...p,name:'retry'}));
});
test('real transformed project is exported for the C# compiler and HTTP validation',()=>{
  let p=example();p=edits.renameContent(p,'skill','basic-shot','authoring-basic');p=edits.renameContent(p,'buff','burn','authoring-burn');p=edits.renameContent(p,'blueprint','echo-gunner','authoring-summon');
  const duplicate=edits.duplicateContent(p,'skill','authoring-basic','作者复制验证',ids());p=edits.selectGraph(duplicate.project,duplicate.id);
  const output=path.resolve(__dirname,'../../.local/verification');fs.mkdirSync(output,{recursive:true});fs.writeFileSync(path.join(output,'authoring-transformed.json'),JSON.stringify(p,null,2));
  assert.equal(p.skills.length,49);
});


test('merge keeps each imported actor bound to its own view and preserves action semantics',()=>{
  const p=runtime(),before=JSON.stringify(p),next=mergeProjects(p,p,ids());
  assert.equal(next.runtime.views.length,p.runtime.views.length*2);
  const imported=next.runtime.actors.slice(p.runtime.actors.length).find(a=>a.role==='player'&&a.id!== 'fighter'&&a.view!=='fighter');
  assert(imported);assert.notEqual(imported.view,'arena-player-view');
  const view=next.runtime.views.find(v=>v.id===imported.view);assert.equal(view.resource,'Combat/Actors/FemaleGunner');assert.equal(view.states.Fire,'Base Layer.Fire');
  assert.equal(next.skills.slice(p.skills.length).find(s=>s.name===p.skills.find(s=>s.id==='arena-reload-ammo').name).timeline.animation,'Reload');
  assert.equal(JSON.stringify(p),before);
});

test('merging body resources rebinds every imported projectile area and construct to the imported catalog',()=>{
  const p=runtime(),next=mergeProjects(p,p,ids()),views=new Set(next.runtime.views.slice(p.runtime.views.length).map(v=>v.id));
  const graphs=[...next.skills.slice(p.skills.length),...next.projectiles.slice(p.projectiles.length),...next.areas.slice(p.areas.length)];
  const references=graphs.flatMap(g=>g.nodes).filter(n=>typeof n.parameters.view==='string'&&p.runtime.views.some(v=>v.id===n.parameters.view));
  assert.equal(references.length,0);
  assert(graphs.flatMap(g=>g.nodes).filter(n=>n.parameters.view&&n.parameters.view!=='barrel'&&n.parameters.view!=='crate').every(n=>views.has(n.parameters.view)));
  const before=p.runtime.views[0].anchors[0].path;next.runtime.views[p.runtime.views.length].anchors[0].path='changed';assert.equal(p.runtime.views[0].anchors[0].path,before);
});


test('copying a moving absorption area preserves its independent definition and absorb branch',()=>{
  const p=example(),s=p.skills.find(s=>s.id==='absorbing-orb'),area=s.nodes.find(n=>n.id==='orb');
  s.nodes.push({id:'absorb-cue',kind:'cue',position:{x:900,y:400},parameters:{id:101,name:'Absorb'}});s.edges.push({id:'absorb-branch',source:area.id,sourcePort:'absorb',target:'absorb-cue',targetPort:'execute',order:0});
  const copy=edits.duplicateContent(p,'skill',s.id,'copy',ids()).project.skills.at(-1),other=copy.nodes.find(n=>n.kind==='delivery');
  assert.notEqual(other.runtimeId,area.runtimeId);assert.equal(other.parameters.areaMotion,'forward');assert.equal(other.parameters.absorbForce,.05);assert(copy.edges.some(e=>e.source===other.id&&e.sourcePort==='absorb'));
  other.parameters.radiusPerProjectile=1;assert.equal(area.parameters.radiusPerProjectile,.05);
});

test('repeat-contact projectile policy survives merge and clipboard graph transfer',()=>{
  const p=example(),next=mergeProjects(p,p,ids()),imported=next.skills.slice(p.skills.length).find(s=>s.name===p.skills.find(s=>s.id==='repeat-contact').name);
  const shot=imported.nodes.find(n=>n.kind==='projectile');assert.equal(shot.parameters.sameTargetDelay,.15);assert.equal(shot.parameters.passInvulnerable,true);
  const copy=graph.copySelection(imported,[shot.id]);assert.equal(copy.nodes[0].parameters.sameTargetDelay,.15);
});


const timelineEdits=require('../../.local/web-checks/authoring/timelineEdits.js');
test('shared timeline extraction moves original behavior once and undo restores its original owner',()=>{
  const p=runtime(),source=p.skills.find(s=>s.id==='arena-controlled-shot'),history=new EditorHistory(p);
  const result=timelineEdits.extractTimeline(p,source.id,()=> 'extracted');history.edit(result.project);
  const action=result.project.timelines.find(t=>t.id===result.id),skill=result.project.skills.find(s=>s.id===source.id);
  assert.equal(skill.timelineRef,'extracted');assert.equal(skill.timeline,undefined);assert(skill.nodes.every(n=>['target','cast'].includes(n.kind)));
  assert.equal(action.timeline.runtimeId,source.timeline.runtimeId);assert.equal(action.nodes.find(n=>n.kind==='projectile').runtimeId,source.nodes.find(n=>n.kind==='projectile').runtimeId);
  assert.equal(result.project.editor.selectedTimeline,'extracted');history.undo();assert.deepEqual(history.current,p);
});
test('duplicating a bound skill shares its action while duplicating the action allocates independent definitions',()=>{
  const p=runtime(),copy=edits.duplicateContent(p,'skill','arena-combo-second','copy',ids());
  assert.equal(copy.project.skills.at(-1).timelineRef,'arena-confirmed-shot');assert.equal(copy.project.timelines.length,1);
  const other=edits.duplicateContent(p,'timeline','arena-confirmed-shot','new action',ids()),action=other.project.timelines.at(-1),original=p.timelines[0];
  assert.notEqual(action.timeline.runtimeId,original.timeline.runtimeId);assert.equal(action.nodes[0].parameters.projectile,original.nodes[0].parameters.projectile);
  assert(action.timeline.payloads[0].effects.every(id=>action.nodes.some(n=>n.id===id)));
});
test('shared action rename rewrites users and editor identity and deletion requires explicit replacement',()=>{
  let p=runtime();p=edits.selectGraph(p,'arena-combo-second','','arena-confirmed-shot');p=edits.saveViewport(p,{kind:'timeline',id:'arena-confirmed-shot'},{x:1,y:2,zoom:.4});
  const renamed=edits.renameContent(p,'timeline','arena-confirmed-shot','shared-next');assert.equal(edits.referencesTo(renamed,'timeline','shared-next').length,2);
  assert.equal(renamed.editor.selectedTimeline,'shared-next');assert.equal(renamed.editor.viewports[0].id,'shared-next');assert.throws(()=>edits.deleteContent(renamed,'timeline','shared-next'),/替代引用/);
  const copy=edits.duplicateContent(renamed,'timeline','shared-next','alternative',ids());const gone=edits.deleteContent(copy.project,'timeline','shared-next',copy.id);assert(gone.skills.filter(s=>s.timelineRef).every(s=>s.timelineRef===copy.id));
});
test('merge rebinds imported shared users and their Buff effects only to imported definitions',()=>{
  const p=runtime(),next=mergeProjects(p,p,ids()),action=next.timelines.at(-1),users=next.skills.slice(p.skills.length).filter(s=>s.timelineRef);
  assert.notEqual(action.id,p.timelines[0].id);assert.equal(users.length,2);assert(users.every(s=>s.timelineRef===action.id));
  const projectile=next.projectiles.find(v=>v.id===action.nodes[0].parameters.projectile);const buff=next.buffs.slice(p.buffs.length).find(b=>b.name===p.buffs.find(b=>b.id==='arena-vulnerability').name);assert.equal(projectile.nodes.find(n=>n.kind==='buff').parameters.buff,buff.id);
  const renamed=edits.renameContent(next,'buff',buff.id,'new-vulnerability');assert.equal(renamed.projectiles.find(v=>v.id===projectile.id).nodes.find(n=>n.kind==='buff').parameters.buff,'new-vulnerability');
});
test('shared graph pasting skips cast roots and keeps effect entries in their own graph',()=>{
  const p=runtime(),key={kind:'timeline',id:'arena-confirmed-shot'},source=p.skills.find(s=>s.id==='arena-controlled-shot');
  const pasted=graph.pasteSelection(p,key,graph.copySelection(source,source.nodes.map(n=>n.id)),{x:100,y:200},ids());
  assert.equal(pasted.skipped,2);assert(pasted.project.timelines[0].nodes.every(n=>!['target','cast'].includes(n.kind)));
  const fresh=timelineEdits.newTimeline(p,'g1',ids());assert(fresh.project.timelines.at(-1).timeline.runtimeId>Math.max(...edits.actionNumbers(p)));
});

test('binding a shared action preserves independent skill lifecycle handlers and cast rules',()=>{
  const p=runtime(),s=p.skills.find(s=>s.id==='g1');s.nodes.push({id:'local-events',kind:'skill-events',position:{x:0,y:0},parameters:{}});
  s.edges.push({id:'local-hit',source:'local-events',sourcePort:'hit',target:'slash-cue',targetPort:'execute',order:0});
  const bound=timelineEdits.bindTimeline(p,'g1','arena-confirmed-shot').skills.find(s=>s.id==='g1');
  assert.equal(bound.timelineRef,'arena-confirmed-shot');assert.equal(bound.timeline,undefined);assert(bound.nodes.some(n=>n.id==='local-events'));assert(bound.nodes.some(n=>n.id==='slash-cue'));
  assert(!bound.nodes.some(n=>n.id==='fireball'));assert(bound.edges.some(e=>e.id==='local-hit'));assert.equal(bound.nodes.find(n=>n.kind==='cast').parameters.cooldown,s.nodes.find(n=>n.kind==='cast').parameters.cooldown);
});


const projectileEdits=require('../../.local/web-checks/authoring/projectileEdits.js');
test('extracting a projectile moves its whole effect branch and preserves timing and emission parameters',()=>{
  const p=example(),s=p.skills.find(s=>s.id==='spread-shot'),root=s.nodes.find(n=>n.kind==='projectile'),before=JSON.stringify(p);
  const result=projectileEdits.extractProjectile(p,{kind:'skill',id:s.id},root.id,()=> 'shared-spread');const def=result.project.projectiles.find(d=>d.id===result.id),source=result.project.skills.find(v=>v.id===s.id);
  assert.equal(def.root,root.id);assert.equal(def.nodes[0].runtimeId,root.runtimeId);assert.equal(def.nodes[0].parameters.multi,undefined);
  const ref=source.nodes.find(n=>n.id===root.id);assert.equal(ref.kind,'projectile-ref');assert.equal(ref.parameters.count,root.parameters.count);assert.equal(ref.parameters.spread,root.parameters.spread);
  assert(def.nodes.some(n=>n.kind==='damage'));assert(!source.nodes.some(n=>n.kind==='damage'));assert.equal(JSON.stringify(p),before);
});
test('extraction rejects effect branches also referenced from outside or by another timeline entry',()=>{
  const p=runtime(),s=p.skills.find(s=>s.id==='arena-controlled-shot'),root=s.nodes.find(n=>n.kind==='projectile'),damage=s.nodes.find(n=>n.kind==='damage');
  s.timeline.payloads[0].effects.push(damage.id);assert.throws(()=>projectileEdits.extractProjectile(p,{kind:'skill',id:s.id},root.id,ids()),/时间轴/);
  s.timeline.payloads[0].effects.pop();s.edges.push({id:'external',source:'cast',sourcePort:'cast',target:damage.id,targetPort:'execute',order:0});assert.throws(()=>projectileEdits.extractProjectile(p,{kind:'skill',id:s.id},root.id,ids()),/其他节点/);
});
test('shared projectile rename rewrites timelines and skills; duplicate definitions get fresh root IDs',()=>{
  const p=runtime(),renamed=edits.renameContent(p,'projectile','arena-normal-projectile','shared-normal');
  assert(edits.referencesTo(renamed,'projectile','shared-normal').length>=2);assert.throws(()=>edits.deleteContent(renamed,'projectile','shared-normal'),/替代引用/);
  const copy=edits.duplicateContent(renamed,'projectile','shared-normal','copy',ids()),old=renamed.projectiles.find(d=>d.id==='shared-normal'),other=copy.project.projectiles.at(-1);
  assert.notEqual(other.root,old.root);assert.notEqual(other.nodes.find(n=>n.id===other.root).runtimeId,old.nodes.find(n=>n.id===old.root).runtimeId);
  const gone=edits.deleteContent(copy.project,'projectile','shared-normal',copy.id);assert(edits.referencesTo(gone,'projectile',copy.id).length>=2);
});
test('merging shared projectiles rebinds every imported use to its own imported definition',()=>{
  const p=runtime(),next=mergeProjects(p,p,ids()),imported=next.projectiles.slice(p.projectiles.length),all=new Set(imported.map(d=>d.id));
  assert(next.skills.slice(p.skills.length).flatMap(s=>s.nodes).filter(n=>n.kind==='projectile-ref').every(n=>all.has(n.parameters.projectile)));
  assert(next.timelines.slice(p.timelines.length).flatMap(s=>s.nodes).filter(n=>n.kind==='projectile-ref').every(n=>all.has(n.parameters.projectile)));
  assert(imported.every(d=>d.nodes.some(n=>n.id===d.root)));assert.equal(new Set(next.projectiles.map(d=>d.nodes.find(n=>n.id===d.root).runtimeId)).size,next.projectiles.length);
});
test('root deletion is protected and scheduled numeric time arrays are independent after copying',()=>{
  const p=runtime(),d=p.projectiles[0],next=graph.deleteSelection(p,{kind:'projectile',id:d.id},[d.root],[]);assert(next.projectiles[0].nodes.some(n=>n.id===d.root));
  const q=example(),s=q.skills.find(s=>s.id==='phase-bounce'),copy=edits.duplicateContent(q,'skill',s.id,'copy',ids()).project.skills.at(-1);copy.nodes.find(n=>n.kind==='projectile').parameters.groundPhases[0]=.5;assert.equal(s.nodes.find(n=>n.kind==='projectile').parameters.groundPhases[0],1);
});
