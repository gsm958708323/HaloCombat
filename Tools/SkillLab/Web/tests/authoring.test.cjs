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
test('deleting an authored effect node removes its timeline entry and undo restores both',()=>{
  const p=runtime(),key={kind:'skill',id:'g1'},history=new EditorHistory(p);
  history.edit(graph.deleteSelection(p,key,['slash-cue'],[]));assert(!history.current.skills[0].timeline.payloads[0].effects.length);
  history.undo();assert.deepEqual(history.current.skills[0].timeline.payloads[0].effects,['slash-cue']);
});
test('merging runtime content preserves authored timing and rewrites all imported Combo and actor references',()=>{
  const p=runtime();const next=mergeProjects(p,p,ids());const imported=next.skills.slice(p.skills.length);const valid=new Set(imported.map(s=>s.id));
  assert(next.runtime.combos.slice(p.runtime.combos.length).every(c=>valid.has(c.toSkill)&&c.preSkills.every(id=>valid.has(id))));
  assert(next.runtime.actors.slice(p.runtime.actors.length).every(a=>(!a.skillId||valid.has(a.skillId))&&(!a.dodgeSkill||valid.has(a.dodgeSkill))&&(a.slots??[]).every(id=>valid.has(id))));
  for(const s of imported)assert([...s.timeline.clips,...s.timeline.payloads].every(c=>(c.effects??[]).every(id=>s.nodes.some(n=>n.id===id))));
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
  assert.equal(p.skills.length,35);
});
