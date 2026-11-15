import type {PreviewState} from '../model';

export function mergePreviewFrame(previous:PreviewState|null,incoming:PreviewState):PreviewState {
  const actors=new Map(previous?.actors.map(a=>[a.id,a]));
  const lastEvent=previous?.events.at(-1)?.sequence??0;
  return {...incoming,actors:incoming.actors.map(actor=>({...actor,
    buffs:actor.buffs??actors.get(actor.id)?.buffs??[],tags:actor.tags??actors.get(actor.id)?.tags??{},
    attributes:actor.attributes??actors.get(actor.id)?.attributes??{}})),
    events:[...(previous?.events??[]),...incoming.events.filter(e=>e.sequence>lastEvent)].slice(-500),
    trails:[]};
}
