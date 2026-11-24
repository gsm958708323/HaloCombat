import type {ComboDecision, PreviewState} from '../model';
import {IDLE_NODE_ID} from './comboModel';
import {comboEdgeId} from './comboLayout';

export interface ActiveComboHighlight {
  skillId: string | null;
  edgeId: string | null;
  actionTime: number | null;
  actionDuration: number | null;
}

export function createActiveComboTracker() {
  let observed: {actorId: string; skillId: string; castId: number; step: number; decision?: ComboDecision} | undefined;
  let lastSnapshot: ActiveComboHighlight = {skillId: null, edgeId: null, actionTime: null, actionDuration: null};

  return (state: PreviewState | null): ActiveComboHighlight => {
    const caster = state?.actors.find(actor => actor.kind === 'caster');
    const skillId = caster?.currentSkill ?? null;
    if (!state || !caster || !skillId) {
      observed = undefined;
      if (lastSnapshot.skillId === null && lastSnapshot.edgeId === null) return lastSnapshot;
      lastSnapshot = {skillId: null, edgeId: null, actionTime: null, actionDuration: null};
      return lastSnapshot;
    }

    if (!observed || observed.actorId !== caster.id || observed.skillId !== skillId ||
      observed.castId !== caster.castId || observed.step > state.step) {
      observed = {actorId: caster.id, skillId, castId: caster.castId, step: state.step};
    }
    observed.step = state.step;

    const events = state.comboDecisions;
    if (events && caster.castId > 0) {
      for (let index = events.length - 1; index >= 0; index--) {
        const event = events[index];
        const decision = event.comboDecision;
        if (event.sourceId !== caster.id || decision?.phase !== 'cast' || decision.castId !== caster.castId) continue;
        observed.decision = decision;
        break;
      }
    }

    const decision = observed.decision;
    let edgeId: string | null = null;
    if (decision?.rule && decision.actualSkill === skillId && decision.toSkill === skillId) {
      edgeId = comboEdgeId(decision.rule, decision.fromSkill || IDLE_NODE_ID);
    }

    const actionTime = caster.actionTime ?? null;
    const actionDuration = caster.actionDuration ?? null;
    if (lastSnapshot.skillId === skillId && lastSnapshot.edgeId === edgeId &&
      lastSnapshot.actionTime === actionTime && lastSnapshot.actionDuration === actionDuration) {
      return lastSnapshot;
    }
    lastSnapshot = {skillId, edgeId, actionTime, actionDuration};
    return lastSnapshot;
  };
}
