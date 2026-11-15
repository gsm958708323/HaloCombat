const test=require('node:test'),assert=require('node:assert/strict');
const {numericText,displayNumber,rangeBounds,rangeValue,steppedValue,formatFieldValue}=require('../../.local/web-checks/authoring/numericParameters.js');
const {EditorHistory}=require('../../.local/web-checks/authoring/EditorHistory.js');
const {kinds}=require('../../.local/web-checks/model.js');

test('fractional saved values survive rendering independently of the suggested slider step',()=>{
  const coefficient={key:'coefficient',label:'攻击系数',initial:0,min:0,max:5,step:.1};
  assert.equal(displayNumber(.05,coefficient),.05);assert.equal(formatFieldValue(coefficient,.05),'0.05');
  assert.deepEqual(rangeBounds(.05,coefficient),{min:0,max:5});assert.equal(steppedValue(.05,coefficient,1),.15);
  assert.equal(steppedValue(.05,coefficient,-1),-.05);assert.equal(rangeValue(.24,coefficient),.2);
});

test('range bounds include valid imported values outside the convenient dragging range',()=>{
  const f={key:'flat',label:'伤害',initial:80,min:0,max:500,step:1};
  assert.deepEqual(rangeBounds(9999,f),{min:0,max:9999});assert.deepEqual(rangeBounds(-3,f),{min:-3,max:500});
  assert.equal(displayNumber(9999,f),9999);assert.equal(steppedValue(9999,f,-1),9998);
});

test('partial invalid and nonfinite text never produce a numeric model update',()=>{
  for(const raw of ['', '-', '.', '1e', '1e-', 'NaN', 'Infinity', '1e999', '0x10', 'abc'])assert.equal(numericText(raw),null,raw);
  assert.equal(numericText('-.125'),-.125);assert.equal(numericText('2.5e-3'),.0025);assert.equal(numericText('1.'),1);
  assert.equal(numericText('1.5',true),null);assert.equal(numericText('3',true),3);
});

test('percentage controls and summaries convert from the single probability value',()=>{
  const f=kinds.damage.fields.find(f=>f.key==='critChance');assert.equal(f.displayScale,100);assert.equal(f.unit,'%');
  assert.equal(displayNumber(.15,f),15);assert.equal(formatFieldValue(f,.15),'15');assert.equal(rangeValue(15.2,f),.15);
  assert.equal(numericText('17')/f.displayScale,.17);assert.equal(steppedValue(.15,f,1),.16);
});

test('integer intent is explicit instead of inferred from the adjustment step',()=>{
  assert.equal(kinds.projectile.fields.find(f=>f.key==='count').integer,true);
  assert.equal(kinds.damage.fields.find(f=>f.key==='flat').integer,undefined);
  assert.equal(kinds['refill-ammo'].fields.find(f=>f.key==='amount').integer,true);
  const f=kinds.projectile.fields.find(f=>f.key==='count');assert(Number.isInteger(rangeValue(2.6,f)));
});

test('separate slider gestures make separate undo steps and text stays one edit transaction',()=>{
  const p={skills:[],preview:{seed:1}},history=new EditorHistory(p);
  const edit=(value,group)=>history.edit({...history.current,preview:{...history.current.preview,seed:value}},group);
  edit(2,'gesture1');edit(3,'gesture1');history.endGroup();edit(4,'gesture2');history.endGroup();
  history.undo();assert.equal(history.current.preview.seed,3);history.undo();assert.equal(history.current.preview.seed,1);
  history.redo();assert.equal(history.current.preview.seed,3);history.redo();assert.equal(history.current.preview.seed,4);
});
