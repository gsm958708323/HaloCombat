import type {ComboDecision, PreviewState} from '../model';
import {comboLinkId} from './comboLayout';
import type {layoutCombo} from './comboLayout';

export function createActiveComboCard(layout: ReturnType<typeof layoutCombo>) {
  let observed: {actorId: string; skillId: string; castId: number; step: number; decision?: ComboDecision} | undefined;
  return (state: PreviewState | null): string | null => {
    const caster = state?.actors.find(actor => actor.kind === 'caster');
    const skillId = caster?.currentSkill;
    if (!state || !caster || !skillId) {
      observed = undefined;
      return null;
    }
    if (!observed || observed.actorId !== caster.id || observed.skillId !== skillId ||
      observed.castId !== caster.castId || observed.step > state.step) {
      observed = {actorId: caster.id, skillId, castId: caster.castId, step: state.step};
    }
    observed.step = state.step;
    // A failed attempt can replace LastDecision without ending the current cast.
    // Remember its successful event only for the lifetime of this same cast.
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
    if (decision?.rule && decision.fromSkill && decision.actualSkill === skillId && decision.toSkill === skillId) {
      const link = layout.linksById.get(comboLinkId(decision.rule, decision.fromSkill));
      if (link?.target === skillId) return link.targetId;
    }
    return layout.mainCards.get(skillId)?.id ?? null;
  };
}
