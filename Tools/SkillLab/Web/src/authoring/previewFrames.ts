import type {PreviewState} from '../model';

export function mergePreviewFrame(previous:PreviewState|null,incoming:PreviewState):PreviewState {
  const actors=new Map(previous?.actors.map(a=>[a.id,a]));
  const lastEvent=previous?.events.at(-1)?.sequence??0;
  const history=previous?.events??[];
  const fresh=incoming.events.filter(event=>event.sequence>lastEvent);
  return {...incoming,actors:incoming.actors.map(actor=>({...actor,
    buffs:actor.buffs??actors.get(actor.id)?.buffs??[],tags:actor.tags??actors.get(actor.id)?.tags??{},
    attributes:actor.attributes??actors.get(actor.id)?.attributes??{}})),
    events:fresh.length?[...history,...fresh].slice(-500):history,
    trails:[]};
}
