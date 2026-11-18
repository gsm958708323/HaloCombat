// These cross-node cases use only the standalone prototype and never read or write a user project.
const assert = require('node:assert/strict');
const test = require('node:test');
const path = require('node:path');
const fs = require('node:fs');
const vm = require('node:vm');
const browser = { console };
browser.window = browser;
vm.createContext(browser);
for (const file of ['model.js', 'simulation.js']) {
  vm.runInContext(fs.readFileSync(path.join(__dirname, file), 'utf8'), browser, {filename: file});
}
const M = browser.LabModel, Sim = browser.LabSim;
const snapshot = (id) => ({doc:M.create(id), setup:M.setup(id), shared:M.sharedDefaults()});
const run = ({doc, setup, shared}) => Sim.run(doc, setup, shared);
const end = (trial, id) => trial.frames.at(-1).actors.find(actor => actor.id === id);
const stateKey = v => JSON.stringify(v);

test('all starting points can be saved and produce reproducible trials', () => {
  for (const {id} of M.templates) {
    const draft = snapshot(id), before=stateKey(draft), first=run(draft), second=run(draft);
    assert.equal(stateKey(first),stateKey(second),id);
    assert.equal(stateKey(draft),before,`the engine edited ${id}`);
    assert(first.duration>0 && first.frames.length>1 && first.events.length>0,id);
    assert(first.events.every(e=>Number.isFinite(e.time) && e.time>=0 && e.time<=first.duration+.001),id);
    assert.equal(end(first,'A').id,'A'); assert.equal(end(first,'B').id,'B');
  }
});

test('full shield absorbs a hit; only a target losing health receives shared burn', () => {
  const draft=snapshot('blast'), b=draft.setup.targets.find(t=>t.id==='B');
  b.shield=1000;
  const hit=run(draft), blocked=end(hit,'B');
  assert.equal(blocked.hp,b.hp);assert.equal(blocked.burn,0);assert(blocked.shield<b.shield);
  b.shield=20;
  const partial=end(run(draft),'B');assert(partial.hp<b.hp);assert.equal(partial.shield,0);assert(partial.burn>0);
  assert.equal(end(hit,'B').burn,0,'a prior trial must remain inspectable');
});

test('burn uses the shared definition for melee, blast and field; separate definitions can diverge', () => {
  for (const id of ['melee','blast','field']) {
    const draft=snapshot(id); const n=draft.doc.nodes.find(n=>n.kind==='burn');assert(n,id);
    const first=run(draft);draft.shared.burn.stacks=2;const second=run(draft);
    assert(end(second,'A').burn>end(first,'A').burn,id);
    const before=stateKey(second);draft.shared.private={name:'独立燃烧',stacks:1};n.params.shared='private';
    assert.equal(end(run(draft),'A').burn,end(first,'A').burn,id);assert.equal(stateKey(second),before);
  }
});

test('adding pierce after contact reaches the second target and a radius hit cannot grant penetration', () => {
  const draft=snapshot('bullet');
  const baseline=end(run(draft),'B');
  assert.equal(baseline.hp,draft.setup.targets[1].hp);
  const options=M.choices(draft.doc,'bullet','hit');assert(options.find(o=>o.kind==='pierce').enabled);
  assert.equal(M.choices(draft.doc,'cast','start').find(o=>o.kind==='pierce').enabled,false);
  draft.doc.nodes.push(M.makeNode('pierce','p'));draft.doc.edges.push({from:'bullet',port:'hit',to:'p'});
  const pierced=run(draft);assert(end(pierced,'B').hp<baseline.hp);
  const explosion=snapshot('blast').doc;
  assert(M.choices(explosion,'bullet','hit').find(o=>o.kind==='blast').enabled);
  assert(!M.choices(explosion,'blast','hit').find(o=>o.kind==='pierce').enabled);
});

test('melee hits once per window and the moving field target can leave and return', () => {
  const m=snapshot('melee');const sweep=run(m);
  const hitsA=sweep.events.filter(e=>e.kind==='hit'&&e.target==='A');assert.equal(hitsA.length,1);
  assert(hitsA[0].time>=m.doc.nodes.find(n=>n.kind==='melee').params.windup);
  const f=snapshot('field');const flame=run(f);const b=flame.events.filter(e=>e.target==='B');
  const visits=b.filter(e=>['enter','exit'].includes(e.kind)).map(e=>e.kind);
  assert.deepEqual(Array.from(visits.slice(0,3)),['enter','exit','enter']);
  let inside=false;
  for(const e of b){if(e.kind==='enter')inside=true;if(e.kind==='exit')inside=false;if(e.kind==='tick')assert(inside);}
});
