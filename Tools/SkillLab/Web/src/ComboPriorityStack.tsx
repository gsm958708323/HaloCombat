import {ArrowDown, ArrowUp, AlertTriangle} from 'lucide-react';
import type {Project, RuntimeCombo} from './model';
import {conditionBadge, priorityConflict} from './authoring/comboModel';

type Props = {
  project: Project;
  allRules: readonly RuntimeCombo[];
  peers: readonly RuntimeCombo[];
  currentRuleId: string;
  onSelectRule: (ruleId: string) => void;
  onReorder: (orderedRuleIds: string[]) => void;
};

export function ComboPriorityStack({project, allRules, peers, currentRuleId, onSelectRule, onReorder}: Props) {
  if (peers.length < 2) return null;
  const skillNames = new Map(project.skills.map(s => [s.id, s.name]));
  const hasAnyConflict = peers.some(rule => !!priorityConflict(allRules, rule));

  function move(index: number, delta: -1 | 1) {
    const nextIndex = index + delta;
    if (nextIndex < 0 || nextIndex >= peers.length) return;
    const ids = peers.map(p => p.id);
    [ids[index], ids[nextIndex]] = [ids[nextIndex], ids[index]];
    onReorder(ids);
  }

  return (
    <div className={`combo-priority-stack ${hasAnyConflict ? 'has-conflict' : ''}`}>
      <div className="combo-field-head">
        <span>同按键求值顺序（高优先先匹配）</span>
        {hasAnyConflict && (
          <button type="button" className="combo-fix-conflict" onClick={() => onReorder(peers.map(p => p.id))}>
            <AlertTriangle size={11}/> 一键消歧
          </button>
        )}
      </div>
      <div className="combo-peer-list">
        {peers.map((peer, index) => {
          const isCurrent = peer.id === currentRuleId;
          const conflict = !!priorityConflict(allRules, peer);
          const badges = (peer.conditions ?? []).map(c => conditionBadge(c, project));
          return (
            <div key={peer.id} className={`combo-peer-row ${isCurrent ? 'is-current' : ''} ${conflict ? 'is-conflict' : ''}`}
              onClick={() => onSelectRule(peer.id)}>
              <span className="combo-peer-rank">#{index + 1}</span>
              <div className="combo-peer-info">
                <strong>{skillNames.get(peer.toSkill) ?? peer.toSkill}</strong>
                <small>
                  {badges.length ? badges.map(b => `${b.icon}${b.text}`).join(' · ') : '无条件默认'}
                </small>
              </div>
              <span className="combo-peer-pri" title="优先级数值">P{peer.priority}</span>
              <div className="combo-peer-arrows" onClick={e => e.stopPropagation()}>
                <button type="button" disabled={index === 0} title="提高优先级" aria-label="提高优先级" onClick={() => move(index, -1)}>
                  <ArrowUp size={11}/>
                </button>
                <button type="button" disabled={index === peers.length - 1} title="降低优先级" aria-label="降低优先级" onClick={() => move(index, 1)}>
                  <ArrowDown size={11}/>
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
