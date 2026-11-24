import {useMemo, useState} from 'react';
import {ArrowRight, ExternalLink, Layers, Plus, Trash2, X} from 'lucide-react';
import type {ComboWindow, ComboWindowMode, Project, RuntimeCombo, Skill} from './model';
import {
  CANCEL_CHANNELS, conflictingPeers, IDLE_NODE_ID, inputColor, inputFullLabel,
  knownCancelChannels, knownInputTokens, priorityConflict, skillMeta,
} from './authoring/comboModel';
import {parseRuleJson} from './authoring/comboEdits';
import {ComboConditionEditor} from './ComboConditionEditor';
import {ComboPriorityStack} from './ComboPriorityStack';

type Props = {
  project: Project;
  rules: readonly RuntimeCombo[];
  cancelRuleIds: ReadonlySet<string>;
  selectedRule?: RuntimeCombo;
  selectedSkill?: Skill;
  isIdleSelected?: boolean;
  onUpdateRule: (ruleId: string, patch: Partial<RuntimeCombo>, autoResolve?: boolean) => void;
  onDeleteRule: (ruleId: string) => void;
  onToggleCancelBar: (ruleId: string) => void;
  onReorderPeers: (orderedIds: string[]) => void;
  onSelectRule: (ruleId: string) => void;
  onAddEntry: (toSkill?: string) => void;
  onAddTransition: (fromSkill: string) => void;
  onToggleSkillCancel: (cancelRuleId: string, skillId: string) => void;
  onOpenSkill: (skillId: string) => void;
  report: (message: string) => void;
};

export function ComboInspector({
  project, rules, cancelRuleIds, selectedRule, selectedSkill, isIdleSelected,
  onUpdateRule, onDeleteRule, onToggleCancelBar, onReorderPeers,
  onSelectRule, onAddEntry, onAddTransition, onToggleSkillCancel, onOpenSkill, report,
}: Props) {
  const [customInputMode, setCustomInputMode] = useState(false);
  const [jsonDraft, setJsonDraft] = useState<string | null>(null);
  const [jsonError, setJsonError] = useState('');

  const skillNames = useMemo(() => new Map(project.skills.map(s => [s.id, s.name])), [project.skills]);
  const tokens = useMemo(() => knownInputTokens(project), [project]);
  const channels = useMemo(() => knownCancelChannels(project), [project]);

  if (selectedRule) {
    const isEntry = !selectedRule.preSkills.length;
    const isCancel = !isEntry && cancelRuleIds.has(selectedRule.id);
    const color = inputColor(selectedRule.input);
    const peers = conflictingPeers(rules, selectedRule);
    const conflict = priorityConflict(rules, selectedRule);
    const windowMode: ComboWindowMode = selectedRule.window?.mode ?? 'default';
    const rawJson = jsonDraft ?? JSON.stringify(selectedRule, null, 2);

    const setWindowMode = (mode: ComboWindowMode) => {
      if (mode === 'default') {
        onUpdateRule(selectedRule.id, {window: undefined}, false);
      } else if (mode === 'channel') {
        onUpdateRule(selectedRule.id, {window: {mode: 'channel', channel: selectedRule.window?.channel || 'dodge'}}, false);
      } else if (mode === 'range') {
        const preSkill = project.skills.find(s => s.id === selectedRule.preSkills[0]);
        const dur = preSkill ? skillMeta(project, preSkill).duration : 0.6;
        const start = selectedRule.window?.start ?? Number((dur * 0.4).toFixed(2));
        const end = selectedRule.window?.end ?? Number(Math.max(start + 0.1, dur * 0.9).toFixed(2));
        onUpdateRule(selectedRule.id, {window: {mode: 'range', start, end}}, false);
      } else {
        onUpdateRule(selectedRule.id, {window: {mode: 'anytime'}}, false);
      }
    };

    const updateWindow = (patch: Partial<ComboWindow>) => {
      const base: ComboWindow = selectedRule.window ?? {mode: 'default'};
      onUpdateRule(selectedRule.id, {window: {...base, ...patch}}, false);
    };

    const availablePreSkills = project.skills.filter(s => !selectedRule.preSkills.includes(s.id));

    return (
      <aside className="combo-inspector" aria-label="连招规则检查器">
        <header className="combo-insp-header">
          <span className={`combo-kind-pill ${isEntry ? 'entry' : isCancel ? 'cancel' : 'transition'}`}>
            {isEntry ? '空闲起手' : isCancel ? '公共取消' : '动作接续'}
          </span>
          <span className="grow"/>
          {!isEntry && (
            <button type="button" className={`combo-insp-toggle ${isCancel ? 'is-on' : ''}`}
              title={isCancel ? '移回主画布作为连线显示' : '收纳至顶部公共取消栏'}
              onClick={() => onToggleCancelBar(selectedRule.id)}>
              <Layers size={12}/> {isCancel ? '取消栏中' : '收进取消栏'}
            </button>
          )}
          <button type="button" className="icon-btn danger" title="删除此规则" aria-label="删除规则"
            onClick={() => onDeleteRule(selectedRule.id)}>
            <Trash2 size={13}/>
          </button>
        </header>

        <div className="combo-insp-flow">
          <span>{isEntry ? '空闲态' : (selectedRule.preSkills.length === 1 ? (skillNames.get(selectedRule.preSkills[0]) ?? selectedRule.preSkills[0]) : `${selectedRule.preSkills.length} 个前置技能`)}</span>
          <ArrowRight size={13}/>
          <strong>{skillNames.get(selectedRule.toSkill) ?? selectedRule.toSkill}</strong>
        </div>

        <div className="combo-insp-body">
          <div className="combo-field">
            <div className="combo-field-head">
              <span>触发逻辑输入</span>
              <button type="button" className="link-btn" onClick={() => setCustomInputMode(v => !v)}>
                {customInputMode ? '从列表选' : '自定义令牌'}
              </button>
            </div>
            <div className="combo-input-row">
              <i className="combo-color-dot" style={{background: color}}/>
              {customInputMode || !tokens.includes(selectedRule.input) ? (
                <input aria-label="逻辑输入令牌" value={selectedRule.input}
                  onChange={e => onUpdateRule(selectedRule.id, {input: e.target.value})}/>
              ) : (
                <select aria-label="逻辑输入令牌" value={selectedRule.input}
                  onChange={e => onUpdateRule(selectedRule.id, {input: e.target.value})}>
                  {tokens.map(token => <option key={token} value={token}>{inputFullLabel(project, token)}</option>)}
                </select>
              )}
            </div>
          </div>

          <div className="combo-field">
            <div className="combo-field-head">
              <span>目标技能</span>
              <button type="button" className="link-btn" onClick={() => onOpenSkill(selectedRule.toSkill)}>
                打开节点图 <ExternalLink size={10}/>
              </button>
            </div>
            <select aria-label="目标技能" value={selectedRule.toSkill}
              onChange={e => onUpdateRule(selectedRule.id, {toSkill: e.target.value}, false)}>
              {project.skills.map(s => <option key={s.id} value={s.id}>{s.name} ({s.id})</option>)}
            </select>
          </div>

          {!isEntry && (
            <div className="combo-field">
              <div className="combo-field-head">
                <span>前置技能（满足任一）</span>
                <small>{selectedRule.preSkills.length} 个</small>
              </div>
              <div className="combo-chip-wrap">
                {selectedRule.preSkills.map(preId => (
                  <span key={preId} className="combo-pre-chip">
                    {skillNames.get(preId) ?? preId}
                    {selectedRule.preSkills.length > 1 && (
                      <button type="button" aria-label={`移除前置 ${preId}`}
                        onClick={() => onUpdateRule(selectedRule.id, {preSkills: selectedRule.preSkills.filter(id => id !== preId)})}>
                        <X size={10}/>
                      </button>
                    )}
                  </span>
                ))}
              </div>
              {availablePreSkills.length > 0 && (
                <select aria-label="添加前置技能" value=""
                  onChange={e => {
                    if (e.target.value) onUpdateRule(selectedRule.id, {preSkills: [...selectedRule.preSkills, e.target.value]});
                  }}>
                  <option value="">+ 添加前置技能…</option>
                  {availablePreSkills.map(s => <option key={s.id} value={s.id}>{s.name} ({s.id})</option>)}
                </select>
              )}
            </div>
          )}

          {!isEntry && (
            <div className="combo-field">
              <div className="combo-field-head"><span>接续窗口时机</span></div>
              <div className="combo-segmented full">
                {([
                  ['default', '通用窗'],
                  ['channel', '命名轨'],
                  ['range', '自定义时段'],
                  ['anytime', '任意时刻'],
                ] as [ComboWindowMode, string][]).map(([mode, label]) => (
                  <button key={mode} type="button" className={windowMode === mode ? 'is-active' : ''}
                    onClick={() => setWindowMode(mode)}>{label}</button>
                ))}
              </div>
              {windowMode === 'channel' && (
                <div className="combo-window-sub">
                  <select aria-label="取消窗轨道名称" value={selectedRule.window?.channel ?? 'dodge'}
                    onChange={e => updateWindow({channel: e.target.value})}>
                    {channels.map(ch => {
                      const preset = CANCEL_CHANNELS.find(c => c.id === ch);
                      return <option key={ch} value={ch}>{preset ? preset.label : ch}</option>;
                    })}
                  </select>
                  <input aria-label="自定义轨道名" placeholder="或输入轨道名…" value={selectedRule.window?.channel ?? ''}
                    onChange={e => updateWindow({channel: e.target.value})}/>
                </div>
              )}
              {windowMode === 'range' && (
                <div className="combo-window-range">
                  <label>开始 (秒)
                    <input type="number" min={0} max={120} step={0.02}
                      value={selectedRule.window?.start ?? 0}
                      onChange={e => {
                        const start = Math.max(0, e.target.valueAsNumber || 0);
                        const end = Math.max(start + 0.02, selectedRule.window?.end ?? start + 0.2);
                        updateWindow({start: Number(start.toFixed(2)), end: Number(end.toFixed(2))});
                      }}/>
                  </label>
                  <label>结束 (秒)
                    <input type="number" min={0.02} max={120} step={0.02}
                      value={selectedRule.window?.end ?? 0.5}
                      onChange={e => {
                        const start = selectedRule.window?.start ?? 0;
                        const end = Math.max(start + 0.02, e.target.valueAsNumber || start + 0.02);
                        updateWindow({start: Number(start.toFixed(2)), end: Number(end.toFixed(2))});
                      }}/>
                  </label>
                </div>
              )}
              {windowMode === 'anytime' && (
                <small className="combo-hint">忽略时间轴取消窗，只要角色状态与资源允许即可立即切换。</small>
              )}
            </div>
          )}

          <ComboConditionEditor project={project} conditions={selectedRule.conditions ?? []}
            onChange={conditions => onUpdateRule(selectedRule.id, {conditions}, false)}/>

          <ComboPriorityStack project={project} allRules={rules} peers={peers} currentRuleId={selectedRule.id}
            onSelectRule={onSelectRule} onReorder={onReorderPeers}/>

          <div className="combo-field">
            <div className="combo-field-head">
              <span>优先级数值</span>
              {conflict && <small className="combo-error-inline">与 {conflict.id} 冲突</small>}
            </div>
            <input type="number" step={1} aria-label="规则优先级" value={selectedRule.priority}
              onChange={e => {
                if (Number.isFinite(e.target.valueAsNumber)) {
                  onUpdateRule(selectedRule.id, {priority: Math.trunc(e.target.valueAsNumber)}, false);
                }
              }}/>
          </div>

          <details className="combo-raw-drawer" onToggle={() => { setJsonDraft(null); setJsonError(''); }}>
            <summary>高级 · 规则 ID 与原始 JSON</summary>
            <label className="combo-field">
              <span>规则 ID</span>
              <input value={selectedRule.id} onChange={e => {
                const nextId = e.target.value.trim();
                if (nextId && !rules.some(r => r.id === nextId)) onUpdateRule(selectedRule.id, {id: nextId}, false);
              }}/>
            </label>
            <textarea aria-label="规则原始 JSON" spellCheck={false} value={rawJson}
              onChange={e => { setJsonDraft(e.target.value); setJsonError(''); }}/>
            <div className="combo-raw-actions">
              <button type="button" className="primary" disabled={jsonDraft === null} onClick={() => {
                try {
                  const parsed = parseRuleJson(rawJson);
                  if (parsed.id !== selectedRule.id && rules.some(r => r.id === parsed.id)) {
                    throw new Error('规则 ID 已被其他规则使用');
                  }
                  onUpdateRule(selectedRule.id, parsed, false);
                  setJsonDraft(null);
                  setJsonError('');
                  report('已从 JSON 应用规则');
                } catch (error) {
                  setJsonError(error instanceof Error ? error.message : String(error));
                }
              }}>应用 JSON</button>
              {jsonDraft !== null && <button type="button" onClick={() => { setJsonDraft(null); setJsonError(''); }}>还原</button>}
            </div>
            {jsonError && <p className="combo-error-inline" role="alert">{jsonError}</p>}
          </details>
        </div>
      </aside>
    );
  }

  if (isIdleSelected) {
    const entries = rules.filter(r => !r.preSkills.length);
    return (
      <aside className="combo-inspector" aria-label="空闲起手检查器">
        <header className="combo-insp-header">
          <span className="combo-kind-pill entry">空闲起手入口</span>
          <span className="grow"/>
          <button type="button" onClick={() => onAddEntry()} disabled={!project.skills.length}><Plus size={12}/>新增起手</button>
        </header>
        <div className="combo-insp-body">
          <p className="combo-hint">角色处于空闲状态时，按下对应按键将直接触发下列起手技能。</p>
          <div className="combo-link-list">
            {entries.map(rule => (
              <button key={rule.id} type="button" className="combo-link-item" onClick={() => onSelectRule(rule.id)}>
                <i className="combo-color-dot" style={{background: inputColor(rule.input)}}/>
                <span>{inputFullLabel(project, rule.input)}</span>
                <ArrowRight size={11}/>
                <strong>{skillNames.get(rule.toSkill) ?? rule.toSkill}</strong>
              </button>
            ))}
            {!entries.length && <p className="combo-cond-empty">暂无空闲起手绑定</p>}
          </div>
        </div>
      </aside>
    );
  }

  if (selectedSkill) {
    const meta = skillMeta(project, selectedSkill);
    const incoming = rules.filter(r => r.toSkill === selectedSkill.id);
    const outgoing = rules.filter(r => r.preSkills.includes(selectedSkill.id) && !cancelRuleIds.has(r.id));
    const cancelRules = rules.filter(r => cancelRuleIds.has(r.id));

    return (
      <aside className="combo-inspector" aria-label="技能连招检查器">
        <header className="combo-insp-header">
          <span className="combo-skill-dot" style={{background: meta.accent}}/>
          <strong className="combo-insp-title">{selectedSkill.name}</strong>
          <span className="grow"/>
          <button type="button" className="primary" onClick={() => onOpenSkill(selectedSkill.id)}>
            编辑技能 <ExternalLink size={11}/>
          </button>
        </header>

        <div className="combo-insp-meta">
          <span>{meta.typeLabel}</span>
          <span>{meta.timelineKind === 'operation' ? '即时操作' : `${meta.duration.toFixed(2)}s`}</span>
          <span>{meta.timelineKind === 'shared' ? `共享轴 · ${meta.sharedTimelineName}` : meta.timelineKind === 'inline' ? '独立轴' : meta.timelineKind === 'auto' ? '自动时序' : '无轴'}</span>
        </div>

        <div className="combo-insp-body">
          <div className="combo-field">
            <div className="combo-field-head">
              <span>后续派生（出边 · {outgoing.length}）</span>
              <button type="button" className="link-btn" onClick={() => onAddTransition(selectedSkill.id)}>+ 接下一段</button>
            </div>
            <div className="combo-link-list">
              {outgoing.map(rule => (
                <button key={rule.id} type="button" className="combo-link-item" onClick={() => onSelectRule(rule.id)}>
                  <i className="combo-color-dot" style={{background: inputColor(rule.input)}}/>
                  <span>{inputFullLabel(project, rule.input)}</span>
                  <ArrowRight size={11}/>
                  <strong>{skillNames.get(rule.toSkill) ?? rule.toSkill}</strong>
                </button>
              ))}
              {!outgoing.length && <p className="combo-cond-empty">暂无后续接续，可从卡片右侧拖线或点 + 添加</p>}
            </div>
          </div>

          <div className="combo-field">
            <div className="combo-field-head"><span>前置入口（入边 · {incoming.length}）</span></div>
            <div className="combo-link-list">
              {incoming.map(rule => (
                <button key={rule.id} type="button" className="combo-link-item" onClick={() => onSelectRule(rule.id)}>
                  <i className="combo-color-dot" style={{background: inputColor(rule.input)}}/>
                  <span>{!rule.preSkills.length ? '空闲' : rule.preSkills.length === 1 ? (skillNames.get(rule.preSkills[0]) ?? rule.preSkills[0]) : `${rule.preSkills.length}技能`}</span>
                  <ArrowRight size={11}/>
                  <strong>{inputFullLabel(project, rule.input)}</strong>
                </button>
              ))}
              {!incoming.length && <p className="combo-cond-empty">暂无进入此技能的规则</p>}
            </div>
          </div>

          {!!cancelRules.length && (
            <div className="combo-field">
              <div className="combo-field-head"><span>允许的公共取消</span></div>
              <div className="combo-cancel-checks">
                {cancelRules.map(rule => {
                  const checked = rule.preSkills.includes(selectedSkill.id);
                  return (
                    <label key={rule.id} className="combo-cancel-check">
                      <input type="checkbox" checked={checked}
                        disabled={checked && rule.preSkills.length <= 1}
                        onChange={() => onToggleSkillCancel(rule.id, selectedSkill.id)}/>
                      <i className="combo-color-dot" style={{background: inputColor(rule.input)}}/>
                      <span>{inputFullLabel(project, rule.input)} → {skillNames.get(rule.toSkill) ?? rule.toSkill}</span>
                    </label>
                  );
                })}
              </div>
            </div>
          )}
        </div>
      </aside>
    );
  }

  return (
    <aside className="combo-inspector is-empty" aria-label="连招检查器">
      <div className="combo-insp-empty">
        <strong>选择卡片或连线</strong>
        <p>点击技能卡片查看派生与时间轴；点击连线编辑按键、取消窗与条件；从卡片右端拖线可直接建立新接续。</p>
        <small>节点 {IDLE_NODE_ID === '$idle' ? '支持自由拖拽排版与双击进入技能编辑' : ''}</small>
      </div>
    </aside>
  );
}
