const test=require('node:test'),assert=require('node:assert/strict'),path=require('node:path');
const {readProject}=require('./projectFixture.cjs');
const {withComboRuntime,connectCombo,patchCombo,removeCombo,moveComboNode,removeComboSkill,comboSkills,comboStart}=require('../../.local/web-checks/authoring/comboEdits.js');
const {renameContent,deleteContent}=require('../../.local/web-checks/authoring/contentEdits.js');
const examples=()=>readProject(path.resolve(__dirname,'../../../../Content/SkillLab/project.json'));
const {layoutCombo}=require('../../.local/web-checks/authoring/comboLayout.js');
const {comboScope,saveComboView,deleteComboView}=require('../../.local/web-checks/authoring/comboViews.js');
const {overlappingRules,rulePurpose}=require('../../.local/web-checks/authoring/comboRules.js');
const {applyComboRule}=require('../../.local/web-checks/authoring/comboEdits.js');
const {comboTrace}=require('../../.local/web-checks/authoring/comboTrace.js');
const {mergeProjects}=require('../../.local/web-checks/authoring/mergeProjects.js');
const formal=()=>readProject(path.resolve(__dirname,'../../../../Assets/StreamingAssets/Combat/project.json'));

test('formal action scope includes cross-input branches and excludes unrelated skills and public cancels',()=>{
  const p=formal(),before=JSON.stringify(p),view=comboScope(p,'arena-ammo-shot');
  assert.equal(p.runtime.combos.length,24);assert.equal(view.rows.length,4);assert.equal(view.ids.length,5);
  assert.deepEqual([...new Set(view.rows.map(r=>r.input))].sort(),['ArenaArea','ArenaAura','ArenaPrimary']);
  assert(!view.ids.includes('g1'));assert.equal(JSON.stringify(p),before);
  const bomb=comboScope(p,'arena-bomb-shot');assert.deepEqual(bomb.ids,['arena-bomb-shot']);assert.equal(bomb.rows.length,0);assert.equal(bomb.entries.length,1);
});
test('layered layout separates formal scope nodes without using runtime state',()=>{
  const p=formal();
  for(const root of ['arena-ammo-shot','g1','arena-bomb-shot']){
    const view=comboScope(p,root),positions=layoutCombo(view.ids,[...view.entries,...view.rows]),values=[...positions.values()];
    assert.equal(positions.size,view.ids.length+1);
    for(let i=0;i<values.length;i++)for(let j=i+1;j<values.length;j++)assert(Math.abs(values[i].x-values[j].x)>=200||Math.abs(values[i].y-values[j].y)>=110);
  }
  const view=comboScope(p,'arena-ammo-shot'),positions=layoutCombo(view.ids,[...view.entries,...view.rows]);
  assert(positions.get('arena-ammo-shot').x>positions.get(comboStart).x);
  for(const row of view.rows)assert(positions.get(row.toSkill).x>positions.get(row.preSkills[0]).x);
});
test('connections and reference removal affect only the selected input chain',()=>{
  let p=examples();p=connectCombo(p,comboStart,'basic-shot','first','Heavy');p=connectCombo(p,comboStart,'basic-shot','second','Attack');
  assert.equal(p.runtime.combos[0].input,'Heavy');const result=removeComboSkill(p,'basic-shot','Heavy');
  assert.equal(result.runtime.combos.length,1);assert.equal(result.runtime.combos[0].input,'Attack');assert.equal(result.skills.length,p.skills.length);
});
test('ordinary author projects acquire a minimal combo runtime without inventing Arena actors',()=>{
  const p=examples(),before=JSON.stringify(p),next=withComboRuntime(p);
  assert.equal(JSON.stringify(p),before);assert(next.runtime);assert.deepEqual(next.runtime.actors,[]);assert.equal(next.runtime.arena,undefined);
  const cue=p.buffs.flatMap(b=>b.nodes).find(n=>n.kind==='cue');assert(next.runtime.cues.some(c=>c.id===cue.parameters.id));
});
test('combo graph supports roots branches and cycles by referencing existing skills',()=>{
  let p=examples();p=connectCombo(p,comboStart,'basic-shot','root');p=connectCombo(p,'basic-shot','burn-shot','branch');p=connectCombo(p,'burn-shot','basic-shot','loop');
  assert.deepEqual(p.runtime.combos[0].preSkills,[]);assert.equal(p.runtime.combos.length,3);assert.equal(p.skills.length,53);
  assert.throws(()=>connectCombo(p,'basic-shot','missing'),/现有技能/);assert.throws(()=>connectCombo(p,'basic-shot',comboStart),/起手/);
  p=patchCombo(p,'branch',{input:'Heavy',requiredTags:[1017],priority:20});assert.equal(p.runtime.combos[1].input,'Heavy');assert.equal(removeCombo(p,'loop').runtime.combos.length,2);
});
test('combo positions are editor metadata and rename or deletion keep them valid',()=>{
  let p=examples();p=moveComboNode(p,'basic-shot',{x:20,y:30});assert(comboSkills(p).includes('basic-shot'));
  const renamed=renameContent(p,'skill','basic-shot','renamed');assert.equal(renamed.editor.comboPositions[0].skillId,'renamed');
  const gone=deleteContent(renamed,'skill','renamed','burn-shot');assert(!gone.editor.comboPositions.some(n=>n.skillId==='renamed'));
});
test('removing a graph reference preserves its reusable skill and removes only associated transitions',()=>{
  let p=connectCombo(examples(),comboStart,'basic-shot','root');p=connectCombo(p,'basic-shot','burn-shot','next');
  const result=removeComboSkill(p,'basic-shot');assert.equal(result.skills.length,p.skills.length);assert.equal(result.runtime.combos.length,0);
  assert.equal(p.runtime.combos.length,2);
});
test('positive tags do not hide conflicts and new connections never silently get a higher priority',()=>{
  let p=connectCombo(examples(),comboStart,'basic-shot','root','Attack');
  assert.throws(()=>connectCombo(p,comboStart,'burn-shot','other','Attack'),/相同输入/);
  assert.equal(p.runtime.combos[0].priority,0);
  const candidate={id:'candidate',input:'Attack',preSkills:[],toSkill:'burn-shot',priority:1,requiredTags:[999],purpose:'entry'};
  assert.deepEqual(overlappingRules(p,candidate).map(r=>r.id),['root']);
  assert.equal(rulePurpose({...candidate,purpose:undefined,preSkills:['basic-shot']}),'unassigned');
});
test('shared view references follow edits deletion and identity changes without duplicating runtime rules',()=>{
  let p=connectCombo(examples(),'basic-shot','burn-shot','shared','Attack');
  p=saveComboView(p,{id:'v1',name:'派生',rootSkill:'basic-shot',ruleIds:['shared'],positions:[]});
  p=saveComboView(p,{id:'v2',name:'复用派生',rootSkill:'basic-shot',ruleIds:['shared'],positions:[]});
  p=applyComboRule(p,{...p.runtime.combos[0],priority:4});
  assert.equal(p.runtime.combos.length,1);assert(p.editor.comboViews.every(v=>v.ruleIds[0]==='shared'));
  p=renameContent(p,'skill','basic-shot','renamed');assert(p.editor.comboViews.every(v=>v.rootSkill==='renamed'));
  const before=JSON.stringify(p.runtime);const withoutView=deleteComboView(p,'v1');assert.equal(JSON.stringify(withoutView.runtime),before);
  p=removeCombo(p,'shared');assert(p.editor.comboViews.every(v=>v.ruleIds.length===0));
});
test('a saved scope never hides that excluded global transitions still affect its skills',()=>{
  let p=connectCombo(examples(),'basic-shot','burn-shot','one','Attack');
  p=connectCombo(p,'basic-shot','basic-shot','two','Alternate');
  p=saveComboView(p,{id:'view',name:'局部',rootSkill:'basic-shot',ruleIds:['one'],positions:[]});
  const view=comboScope(p,'basic-shot','view');assert.deepEqual(view.rows.map(r=>r.id),['one']);assert.deepEqual(view.external.map(r=>r.id),['two']);
  assert.equal(p.runtime.combos.length,2);
});
test('saved views retain disconnected referenced transitions instead of silently pruning author content',()=>{
  let p=connectCombo(examples(),'basic-shot','burn-shot','one','Attack');
  p=connectCombo(p,'burn-shot','basic-shot','two','Other');
  p=saveComboView(p,{id:'view',name:'作者编排',rootSkill:'basic-shot',ruleIds:['two'],positions:[]});
  assert.deepEqual(comboScope(p,'basic-shot','view').rows.map(r=>r.id),['two']);
});
test('merging conflicting skill rule and view identities rewrites all author references',()=>{
  let source=connectCombo(examples(),'basic-shot','burn-shot','shared','Attack');
  source=saveComboView(source,{id:'view',name:'共享派生',rootSkill:'basic-shot',ruleIds:['shared'],positions:[{skillId:'basic-shot',x:4,y:5}]});
  let counter=0;const merged=mergeProjects(source,source,()=> 'merged-'+(++counter));
  const added=merged.editor.comboViews.find(v=>v.id!=='view'),addedRule=merged.runtime.combos.find(r=>r.id!=='shared');
  assert(added);assert(addedRule);assert.equal(added.ruleIds[0],addedRule.id);assert.equal(added.rootSkill,addedRule.preSkills[0]);
  assert.equal(added.positions[0].skillId,added.rootSkill);
});
test('runtime trace preserves repeated successful skills and separates rejected selected rules',()=>{
  const d=(seq,from,to,phase='cast')=>({sequence:seq,inputSequence:seq,input:'Attack',fromSkill:from,toSkill:to,actualSkill:phase==='cast'?to:null,phase,rule:'r'+seq,candidates:[],windowOpen:true,confirmed:true,reason:null,castId:seq});
  const events=[d(1,null,'g1'),d(2,'g1','g2'),d(3,'g2','g1'),d(4,'g1','g2','rejected')].map((comboDecision,i)=>({sequence:i+2,kind:'combo-decision',sourceId:'caster',comboDecision}));
  const trace=comboTrace({actors:[{id:'caster',kind:'caster',name:'玩家',comboInputSequence:4}],events:[],comboDecisions:events});
  assert.deepEqual(trace.steps.map(s=>s.skill),['g1','g2','g1']);assert.equal(trace.decision.phase,'rejected');
  assert(!trace.executedRules.has('r4'));assert(trace.hasIdleStart);
  assert(trace.executedEdges.has('r3::g2'));assert(!trace.executedEdges.has('r3::g1'));
});
