import {useMemo, useState} from 'react';
import {LocateFixed, Plus, Search, Trash2} from 'lucide-react';
import type {Project, RuntimeCombo} from './model';
import {conditionBadge, inputColor, inputFullLabel, priorityConflict, windowBadge} from './authoring/comboModel';

type Props = {
  project: Project;
  rules: readonly RuntimeCombo[];
  cancelRuleIds: ReadonlySet<string>;
  selectedRuleId?: string;
  onSelectRule: (rule: RuntimeCombo) => void;
  onLocateRule: (rule: RuntimeCombo) => void;
  onDeleteRule: (ruleId: string) => void;
  onAddEntry: () => void;
  onAddTransition: () => void;
  onAddCancel: () => void;
};

type KindFilter = 'all' | 'entry' | 'transition' | 'cancel';

export function ComboRuleTable({
  project, rules, cancelRuleIds, selectedRuleId,
  onSelectRule, onLocateRule, onDeleteRule, onAddEntry, onAddTransition, onAddCancel,
}: Props) {
  const [kind, setKind] = useState<KindFilter>('all');
  const [inputFilter, setInputFilter] = useState('');
  const [query, setQuery] = useState('');

  const skillNames = useMemo(() => new Map(project.skills.map(s => [s.id, s.name])), [project.skills]);
  const inputs = useMemo(() => [...new Set(rules.map(r => r.input))], [rules]);

  const rows = useMemo(() => {
    const q = query.trim().toLowerCase();
    return rules.filter(rule => {
      const ruleKind: KindFilter = !rule.preSkills.length ? 'entry' : cancelRuleIds.has(rule.id) ? 'cancel' : 'transition';
      if (kind !== 'all' && ruleKind !== kind) return false;
      if (inputFilter && rule.input !== inputFilter) return false;
      if (!q) return true;
      const toName = (skillNames.get(rule.toSkill) ?? '').toLowerCase();
      const preNames = rule.preSkills.map(id => (skillNames.get(id) ?? id).toLowerCase()).join(' ');
      return rule.id.toLowerCase().includes(q) || rule.input.toLowerCase().includes(q)
        || rule.toSkill.toLowerCase().includes(q) || toName.includes(q) || preNames.includes(q);
    });
  }, [rules, cancelRuleIds, kind, inputFilter, query, skillNames]);

  return (
    <div className="combo-table-view" aria-label="全表规则总览">
      <div className="combo-table-toolbar">
        <div className="combo-segmented">
          {([['all', '全部'], ['entry', '空闲起手'], ['transition', '动作接续'], ['cancel', '公共取消']] as [KindFilter, string][]).map(([key, label]) => (
            <button key={key} type="button" className={kind === key ? 'is-active' : ''} onClick={() => setKind(key)}>{label}</button>
          ))}
        </div>
        <select aria-label="按输入筛选" value={inputFilter} onChange={e => setInputFilter(e.target.value)}>
          <option value="">全部按键</option>
          {inputs.map(token => <option key={token} value={token}>{inputFullLabel(project, token)}</option>)}
        </select>
        <label className="combo-table-search">
          <Search size={12}/>
          <input value={query} placeholder="筛选技能名 / 规则 ID…" onChange={e => setQuery(e.target.value)}/>
        </label>
        <span className="grow"/>
        <button type="button" onClick={onAddEntry} disabled={!project.skills.length}><Plus size={12}/>起手</button>
        <button type="button" onClick={onAddTransition} disabled={!project.skills.length}><Plus size={12}/>接续</button>
        <button type="button" onClick={onAddCancel} disabled={!project.skills.length}><Plus size={12}/>公共取消</button>
      </div>

      <div className="combo-table-scroll">
        <table className="combo-rule-table">
          <thead>
            <tr>
              <th>类型</th>
              <th>逻辑输入</th>
              <th>前置技能</th>
              <th>目标技能</th>
              <th>条件</th>
              <th>取消窗</th>
              <th>优先级</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            {rows.map(rule => {
              const isEntry = !rule.preSkills.length;
              const isCancel = !isEntry && cancelRuleIds.has(rule.id);
              const color = inputColor(rule.input);
              const conflict = priorityConflict(rules, rule);
              const win = windowBadge(rule.window);
              return (
                <tr key={rule.id} className={`${rule.id === selectedRuleId ? 'is-selected' : ''} ${conflict ? 'is-conflict' : ''}`}
                  onClick={() => onSelectRule(rule)}>
                  <td>
                    <span className={`combo-kind-pill ${isEntry ? 'entry' : isCancel ? 'cancel' : 'transition'}`}>
                      {isEntry ? '起手' : isCancel ? '公共取消' : '接续'}
                    </span>
                  </td>
                  <td>
                    <span className="combo-input-chip" style={{'--chip-color': color} as React.CSSProperties}>
                      <i/>{inputFullLabel(project, rule.input)}
                    </span>
                  </td>
                  <td className="combo-table-pre">
                    {isEntry ? <span className="muted">空闲态</span> : (
                      rule.preSkills.length <= 2
                        ? rule.preSkills.map(id => skillNames.get(id) ?? id).join('、')
                        : `${skillNames.get(rule.preSkills[0]) ?? rule.preSkills[0]} 等 ${rule.preSkills.length} 个`
                    )}
                  </td>
                  <td><strong>{skillNames.get(rule.toSkill) ?? rule.toSkill}</strong></td>
                  <td>
                    <div className="combo-table-badges">
                      {!rule.conditions?.length ? <span className="muted">无</span> : rule.conditions.map((c, idx) => {
                        const badge = conditionBadge(c, project);
                        return <span key={idx} className={`combo-edge-tag tone-${badge.tone}`}>{badge.icon}{badge.text}</span>;
                      })}
                    </div>
                  </td>
                  <td>
                    {isEntry ? <span className="muted">—</span> : win
                      ? <span className="combo-win-pill" style={{borderColor: win.color, color: win.color}}>{win.text}</span>
                      : <span className="muted">通用窗</span>}
                  </td>
                  <td>
                    <span className={conflict ? 'combo-pri-conflict' : ''}>
                      {rule.priority}{conflict ? ' ⚠' : ''}
                    </span>
                  </td>
                  <td className="combo-table-actions" onClick={e => e.stopPropagation()}>
                    <button type="button" title="在画布定位" onClick={() => onLocateRule(rule)}><LocateFixed size={12}/></button>
                    <button type="button" title="删除规则" onClick={() => onDeleteRule(rule.id)}><Trash2 size={12}/></button>
                  </td>
                </tr>
              );
            })}
            {!rows.length && (
              <tr><td colSpan={8} className="combo-table-empty">没有符合条件的连招规则</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
