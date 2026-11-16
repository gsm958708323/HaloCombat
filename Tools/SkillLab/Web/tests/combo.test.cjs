const test=require('node:test'),assert=require('node:assert/strict'),path=require('node:path');
const {readProject}=require('./projectFixture.cjs');
const {withComboRuntime,connectCombo,patchCombo,removeCombo,moveComboNode,removeComboSkill,comboSkills,comboStart}=require('../../.local/web-checks/authoring/comboEdits.js');
const {renameContent,deleteContent}=require('../../.local/web-checks/authoring/contentEdits.js');
const examples=()=>readProject(path.resolve(__dirname,'../../../../Content/SkillLab/project.json'));
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
