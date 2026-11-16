const test=require('node:test'),assert=require('node:assert/strict'),path=require('node:path');
const {readProject}=require('./projectFixture.cjs');
const {withComboRuntime,connectCombo,patchCombo,removeCombo,moveComboNode,removeComboSkill,comboSkills,comboStart}=require('../../.local/web-checks/authoring/comboEdits.js');
const {renameContent,deleteContent}=require('../../.local/web-checks/authoring/contentEdits.js');
const examples=()=>readProject(path.resolve(__dirname,'../../../../Content/SkillLab/project.json'));
const {allComboInputs,comboView,layoutCombo}=require('../../.local/web-checks/authoring/comboLayout.js');
const formal=()=>readProject(path.resolve(__dirname,'../../../../Assets/StreamingAssets/Combat/project.json'));

test('the actual formal table opens one input chain without changing its 24 rules',()=>{
  const p=formal(),before=JSON.stringify(p),view=comboView(p,'');
  assert.equal(view.selected,'Attack');assert.equal(view.rows.length,3);assert.equal(view.ids.length,3);
  assert.equal(view.total,24);assert.equal(comboView(p,allComboInputs).rows.length,24);
  for(const token of view.tokens){const v=comboView(p,token);assert(v.rows.every(row=>row.input===token));
    assert(v.rows.every(row=>v.ids.includes(row.toSkill)&&row.preSkills.every(id=>v.ids.includes(id))));}
  assert.equal(JSON.stringify(p),before);
});
test('layered layout separates every real formal node and places the primary combo left to right',()=>{
  const p=formal();
  for(const token of [...comboView(p,'').tokens,allComboInputs]){
    const view=comboView(p,token),positions=layoutCombo(view.ids,view.rows),values=[...positions.values()];
    assert.equal(positions.size,view.ids.length+1);
    for(let i=0;i<values.length;i++)for(let j=i+1;j<values.length;j++)assert(Math.abs(values[i].x-values[j].x)>=200||Math.abs(values[i].y-values[j].y)>=110);
  }
  const view=comboView(p,'ArenaPrimary'),positions=layoutCombo(view.ids,view.rows);
  const root=view.rows.find(row=>!row.preSkills.length);assert(root);
  assert(positions.get(root.toSkill).x>positions.get(comboStart).x);
  for(const row of view.rows.filter(row=>row.preSkills.length))assert(positions.get(row.toSkill).x>positions.get(row.preSkills[0]).x);
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
