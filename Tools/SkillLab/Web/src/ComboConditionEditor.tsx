import {X} from 'lucide-react';
import type {ComboCompareOp, ComboCondition, Project} from './model';
import {COMBO_CONFIRM_TAG, COMMON_TAGS} from './authoring/comboModel';

type Props = {
  project: Project;
  conditions: readonly ComboCondition[];
  onChange: (next: ComboCondition[]) => void;
};

export function ComboConditionEditor({project, conditions, onChange}: Props) {
  const buffs = project.buffs ?? [];

  function add(cond: ComboCondition) {
    onChange([...conditions, cond]);
  }
  function updateAt(index: number, patch: Partial<ComboCondition>) {
    onChange(conditions.map((item, i) => i === index ? {...item, ...patch} : item));
  }
  function removeAt(index: number) {
    onChange(conditions.filter((_, i) => i !== index));
  }

  return (
    <div className="combo-cond-section">
      <div className="combo-field-head">
        <span>触发条件（全部满足）</span>
        <small>{conditions.length} 项</small>
      </div>

      <div className="combo-cond-presets">
        <button type="button" onClick={() => add({kind: 'tag', tag: COMBO_CONFIRM_TAG})}>+ 命中确认</button>
        <button type="button" onClick={() => add({kind: 'tag', tag: COMBO_CONFIRM_TAG, negate: true})}>+ 挥空</button>
        <button type="button" onClick={() => add({kind: 'tag', tag: 1002})}>+ 状态 Tag</button>
        <button type="button" disabled={!buffs.length} title={buffs.length ? '按施法者 Buff 层数判断' : '当前工程暂无 Buff 定义'}
          onClick={() => buffs[0] && add({kind: 'buff', buff: buffs[0].id, op: 'gte', value: 1})}>+ Buff</button>
        <button type="button" onClick={() => add({kind: 'resource', resource: 'energy', op: 'gte', value: 20})}>+ 能量/弹药</button>
      </div>

      {!conditions.length ? (
        <p className="combo-cond-empty">无附加条件（窗口开启即可接续）</p>
      ) : (
        <div className="combo-cond-list">
          {conditions.map((cond, index) => (
            <div key={index} className={`combo-cond-row ${cond.negate ? 'is-negated' : ''}`}>
              <button type="button" className="combo-cond-negate"
                title="点击切换「满足」或「禁止」"
                onClick={() => updateAt(index, {negate: !cond.negate || undefined})}>
                {cond.negate ? '禁止' : '满足'}
              </button>

              {cond.kind === 'tag' && (
                <div className="combo-cond-controls">
                  <select aria-label="状态 Tag 预设"
                    value={COMMON_TAGS.some(t => t.id === cond.tag) ? String(cond.tag) : 'custom'}
                    onChange={e => {
                      if (e.target.value !== 'custom') updateAt(index, {tag: Number(e.target.value)});
                    }}>
                    {COMMON_TAGS.map(t => <option key={t.id} value={t.id}>{t.label} ({t.id})</option>)}
                    <option value="custom">自定义 Tag…</option>
                  </select>
                  <input type="number" className="combo-num-sm" min={1} step={1} aria-label="Tag ID"
                    value={cond.tag ?? COMBO_CONFIRM_TAG}
                    onChange={e => {
                      const v = Math.max(1, Math.trunc(e.target.valueAsNumber || 1));
                      updateAt(index, {tag: v});
                    }}/>
                </div>
              )}

              {cond.kind === 'buff' && (
                <div className="combo-cond-controls">
                  <select aria-label="Buff 定义" value={cond.buff ?? ''}
                    onChange={e => updateAt(index, {buff: e.target.value})}>
                    {!buffs.some(b => b.id === cond.buff) && cond.buff && <option value={cond.buff}>{cond.buff} (缺失)</option>}
                    {buffs.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}
                  </select>
                  <select aria-label="层数比较" className="combo-op-select" value={cond.op ?? 'gte'}
                    onChange={e => updateAt(index, {op: e.target.value as ComboCompareOp})}>
                    <option value="gte">≥</option>
                    <option value="eq">=</option>
                    <option value="lt">&lt;</option>
                  </select>
                  <input type="number" className="combo-num-xs" min={0} max={128} step={1} aria-label="Buff 层数"
                    value={cond.value ?? 1}
                    onChange={e => {
                      const v = Math.max(0, Math.min(128, Math.trunc(e.target.valueAsNumber || 0)));
                      updateAt(index, {value: v});
                    }}/>
                  <span className="combo-unit">层</span>
                </div>
              )}

              {cond.kind === 'resource' && (
                <div className="combo-cond-controls">
                  <select aria-label="资源类型" value={cond.resource ?? 'energy'}
                    onChange={e => updateAt(index, {resource: e.target.value as 'energy' | 'ammo'})}>
                    <option value="energy">能量</option>
                    <option value="ammo">弹药</option>
                  </select>
                  <select aria-label="资源比较" className="combo-op-select" value={cond.op ?? 'gte'}
                    onChange={e => updateAt(index, {op: e.target.value as ComboCompareOp})}>
                    <option value="gte">≥</option>
                    <option value="lte">≤</option>
                    <option value="eq">=</option>
                    <option value="lt">&lt;</option>
                  </select>
                  <input type="number" className="combo-num-sm" min={0} step={cond.resource === 'ammo' ? 1 : 5} aria-label="资源数值"
                    value={cond.value ?? 0}
                    onChange={e => {
                      const v = Math.max(0, e.target.valueAsNumber || 0);
                      updateAt(index, {value: v});
                    }}/>
                </div>
              )}

              <button type="button" className="icon-btn combo-cond-del" aria-label="移除条件" onClick={() => removeAt(index)}>
                <X size={12}/>
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
