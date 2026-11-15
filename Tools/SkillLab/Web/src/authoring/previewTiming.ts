import type {Project} from '../model';

export function previewSemanticKey(project:Project|null,selected:string):string {
  const graph=<T extends {nodes:{position:unknown}[]}>(value:T)=>({...value,nodes:value.nodes.map(n=>({...n,position:undefined}))});
  return JSON.stringify([selected,project&&{...project,editor:undefined,skills:project.skills.map(graph),buffs:project.buffs?.map(graph),
    timelines:project.timelines?.map(graph),areas:project.areas?.map(graph),projectiles:project.projectiles?.map(graph)}]);
}
export function previewFrameBudget(remainder:number,elapsed:number,speed:number) {
  const carry=Number.isFinite(remainder)?Math.max(0,Math.min(.02,remainder)):0;
  const rate=Number.isFinite(speed)?Math.max(0,Math.min(2,speed)):0;
  const total=carry+Math.max(0,Math.min(.2,Number.isFinite(elapsed)?elapsed:0))*rate;
  const steps=Math.min(20,Math.max(0,Math.floor((total+.00000001)/.02)));
  return {steps,remainder:Math.max(0,Math.min(.02,total-steps*.02))};
}
