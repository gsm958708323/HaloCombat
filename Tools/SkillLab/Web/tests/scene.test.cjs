const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {addSceneTarget,addSceneObstacle,moveSceneActor,removeSceneTarget,scenePlacementFree,sceneBounds,fitScene,zoomScene}=require('../../.local/web-checks/authoring/scenePlacement.js');
const project=()=>require('./projectFixture.cjs').readProject(path.resolve(__dirname,'../../../../Content/SkillLab/project.json'));

test('target and obstacle creation are deterministic independent snapshots with free placement and stable identities',()=>{
  const p=project(),before=JSON.stringify(p),a=addSceneTarget(p),b=addSceneTarget(p);
  assert.deepEqual(a,b);assert.equal(JSON.stringify(p),before);assert.equal(a.preview.targets.length,p.preview.targets.length+1);
  assert.equal(new Set([a.preview.caster.id,...a.preview.targets.map(v=>v.id)]).size,a.preview.targets.length+1);
  const next=addSceneObstacle(a),box=next.preview.obstacles.at(-1);assert(box.id);
  assert([next.preview.caster,...next.preview.targets].every(v=>scenePlacementFree(v,v.collisionRadius??.4,[box])));
});
test('moves preserve other authoring data and reject obstructed positions without partially changing the source',()=>{
  const p=project();p.preview.obstacles=[{id:'wall',x:1,z:0,width:1,depth:4}];const before=JSON.stringify(p);
  const next=moveSceneActor(p,p.preview.caster.id,{x:-5,z:2});assert.equal(next.preview.caster.x,-5);assert.equal(next.preview.caster.z,2);
  assert.deepEqual(next.skills,p.skills);assert.throws(()=>moveSceneActor(p,p.preview.caster.id,{x:1,z:0}),/重叠/);assert.equal(JSON.stringify(p),before);
});
test('target removal preserves caster and refuses to remove the required scene actor',()=>{
  const p=project(),id=p.preview.targets[0].id,next=removeSceneTarget(p,id);assert(!next.preview.targets.some(v=>v.id===id));
  assert.deepEqual(next.preview.caster,p.preview.caster);assert.throws(()=>removeSceneTarget(p,p.preview.caster.id),/不能/);
});
test('reset view contains distant authoring actors and obstacle extents at wide and tall aspect ratios',()=>{
  const p=project();p.preview.targets[0].x=80;p.preview.obstacles=[{id:'far',x:-20,z:30,width:4,depth:6}];const b=sceneBounds(p);
  for(const aspect of [3,.5,16/9]){
    const v=fitScene(b,aspect);assert(Math.abs(v.width/v.height-aspect)<1e-8);assert(v.x<=b.left&&v.x+v.width>=b.right);
    assert(v.y<=-b.top&&v.y+v.height>=-b.bottom);
  }
});
test('zoom preserves the world point beneath the cursor while obeying bounded view widths',()=>{
  const v={x:-10,y:-6,width:20,height:12},p={x:3,z:2},next=zoomScene(v,p,.5);
  assert(Math.abs((p.x-v.x)/v.width-(p.x-next.x)/next.width)<1e-8);assert(Math.abs((-p.z-v.y)/v.height-(-p.z-next.y)/next.height)<1e-8);
  assert.equal(zoomScene(v,p,100).width,160);assert.equal(zoomScene(v,p,.0001).width,6);
});
test('client obstacle guide matches circle geometry and treats nonfinite destinations as invalid',()=>{
  const b=[{id:'wall',x:0,z:0,width:2,depth:2}];assert(!scenePlacementFree({x:1.2,z:0},.4,b));assert(scenePlacementFree({x:1.4,z:0},.4,b));
  assert(!scenePlacementFree({x:NaN,z:0},.4,b));assert(scenePlacementFree({x:2,z:2},.4,b));
});
