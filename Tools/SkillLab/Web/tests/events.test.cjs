const test=require('node:test'),assert=require('node:assert/strict');
const {runtimeEventView,matchesRuntimeEvent}=require('../../.local/web-checks/authoring/runtimeEvents.js');
function event(sequence,step,kind){return {sequence,step,kind};}

test('event categories include actual related kinds instead of only one exact runtime string',()=>{
  for(const kind of ['damage','critical'])assert(matchesRuntimeEvent(kind,'damage'));
  for(const kind of ['cast','operation','completed','cancelled','released','skill-completed'])assert(matchesRuntimeEvent(kind,'cast'));
  for(const kind of ['projectile-rejected','summon-rejected','construct-rejected','skill-event-rejected','projectile-teleport-blocked'])assert(matchesRuntimeEvent(kind,'rejected'));
  assert(matchesRuntimeEvent('projectile-flight','behavior'));assert(matchesRuntimeEvent('buff-expired','lifecycle'));assert(!matchesRuntimeEvent('execute','key'));assert(matchesRuntimeEvent('projectile-chain','key'));
});

test('newer steps are first while events within one step retain actual sequence order',()=>{
  const source=[event(1,1,'cast'),event(2,2,'spawn'),event(3,2,'damage'),event(4,2,'completed'),event(5,3,'cleanup')],before=JSON.stringify(source);
  assert.deepEqual(runtimeEventView(source,'all').rows.map(e=>e.sequence),[5,2,3,4,1]);assert.equal(JSON.stringify(source),before);
});

test('clear checkpoints hide old rows without changing runtime events and new events remain visible',()=>{
  const source=[event(10,1,'damage'),event(11,1,'critical'),event(12,2,'cast')];
  const view=runtimeEventView(source,'damage',11);assert.equal(view.rows.length,0);assert.equal(view.retained.length,1);assert.equal(source.length,3);
  source.push(event(13,3,'damage'));assert.deepEqual(runtimeEventView(source,'damage',11).rows.map(e=>e.sequence),[13]);assert.equal(runtimeEventView(source,'all',0).total,4);
});

test('retained count filtering and display limits describe exactly the rows shown',()=>{
  const source=Array.from({length:100},(_,i)=>event(i+1,i,i%2?'damage':'execute')),view=runtimeEventView(source,'damage',0,20);
  assert.equal(view.total,50);assert.equal(view.retained.length,100);assert.equal(view.rows.length,20);assert.equal(view.hidden,30);assert.equal(runtimeEventView(source,'all',0,0).rows.length,0);
});

const {mergePreviewFrame}=require('../../.local/web-checks/authoring/previewFrames.js');
test('incremental preview keeps diagnostics by full entity identity, bounds events and drops movement trails',()=>{
  const actor={id:'1',attributes:{Atk:80},tags:{101:1},buffs:[{id:'burn'}],x:0,z:0};
  const previous={step:20,actors:[actor],events:Array.from({length:500},(_,i)=>({sequence:i+1})),trails:[{step:20,x:0,z:0}]};
  const incoming={step:21,actors:[{id:'1',x:1,z:0},{id:'2',x:3,z:0}],events:[{sequence:500},{sequence:501}],trails:[{step:21,x:1,z:0}]};
  const merged=mergePreviewFrame(previous,incoming);
  assert.equal(merged.events.length,500);assert.equal(merged.events.at(-1).sequence,501);assert.equal(merged.actors[0].x,1);
  assert.equal(merged.actors[0].attributes,actor.attributes);assert.deepEqual(merged.actors[1].attributes,{});assert.deepEqual(merged.trails,[]);
  assert.equal(previous.actors[0].x,0);assert.equal(previous.events.at(-1).sequence,500);
});
