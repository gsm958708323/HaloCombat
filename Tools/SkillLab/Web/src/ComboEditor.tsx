import {useCallback, useMemo, useState} from 'react';
import {Eye, EyeOff, LayoutGrid, Maximize, Plus, Sparkles, Table2, Workflow} from 'lucide-react';
import type {Project, RuntimeCombo} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';
import {
  detectComboChains, IDLE_NODE_ID, inputColor, inputShortLabel,
} from './authoring/comboModel';
import {
  addCancelRule, addEntryRule, addTransitionRule, clearComboPositions,
  deleteComboRule, reorderPeerPriorities, saveComboPositions,
  toggleCancelBarMembership, toggleCancelRulePreSkill, updateComboRule,
} from './authoring/comboEdits';
import {buildComboDagLayout, comboEdgeId} from './authoring/comboLayout';
import type {ComboDagEdge} from './authoring/comboLayout';
import {ComboGraph} from './ComboGraph';
import {ComboSkillDrawer} from './ComboSkillDrawer';
import {ComboRuleTable} from './ComboRuleTable';
import {ComboTimelineDock} from './ComboTimelineDock';
import {ComboInspector} from './ComboInspector';
import './combo-editor.css';
import './combo-timeline.css';

type Props = {
  project: Project;
  update: (edit: (project: Project) => Project, remember?: boolean, group?: string) => void;
  store: PreviewRuntimeStore;
  openSkill: (skillId: string) => void;
  report: (message: string) => void;
};

type Selection =
  | {kind: 'idle'}
  | {kind: 'skill'; skillId: string}
  | {kind: 'rule'; ruleId: string; edgeId?: string; sourceSkill?: string};

const emptyRules: RuntimeCombo[] = [];

export function ComboEditor({project, update, store, openSkill, report}: Props) {
  const rules = project.runtime?.combos ?? emptyRules;
  const cancelRuleIds = useMemo(() => new Set(project.editor?.comboCancelRuleIds ?? []), [project.editor?.comboCancelRuleIds]);
  const [surface, setSurface] = useState<'canvas' | 'table'>('canvas');
  const [chainFilter, setChainFilter] = useState<string>('all');
  const [showCancelOverlay, setShowCancelOverlay] = useState(false);
  const [activeCancelRuleId, setActiveCancelRuleId] = useState<string | null>(null);
  const [hoveredCancelRuleId, setHoveredCancelRuleId] = useState<string | null>(null);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [pinnedSkills, setPinnedSkills] = useState<string[]>([]);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [fitNonce, setFitNonce] = useState(0);

  const chains = useMemo(() => detectComboChains(project, rules, cancelRuleIds), [project, rules, cancelRuleIds]);
  const activeChain = chains.find(c => c.id === chainFilter);

  const visibleSkillIds = useMemo(() => {
    const validIds = new Set(project.skills.map(s => s.id));
    if (activeChain) {
      return [...activeChain.skillIds].filter(id => validIds.has(id));
    }
    const set = new Set<string>();
    for (const rule of rules) {
      if (validIds.has(rule.toSkill)) set.add(rule.toSkill);
      for (const pre of rule.preSkills) if (validIds.has(pre)) set.add(pre);
    }
    for (const pinned of pinnedSkills) if (validIds.has(pinned)) set.add(pinned);
    for (const pos of project.editor?.comboPositions ?? []) if (validIds.has(pos.skillId)) set.add(pos.skillId);
    return [...set];
  }, [project.skills, project.editor?.comboPositions, rules, activeChain, pinnedSkills]);

  const layout = useMemo(
    () => buildComboDagLayout(rules, visibleSkillIds, cancelRuleIds, project.editor?.comboPositions, showCancelOverlay),
    [rules, visibleSkillIds, cancelRuleIds, project.editor?.comboPositions, showCancelOverlay],
  );

  const cancelRules = useMemo(() => rules.filter(r => cancelRuleIds.has(r.id)), [rules, cancelRuleIds]);
  const effectiveActiveCancelId = activeCancelRuleId && cancelRuleIds.has(activeCancelRuleId) ? activeCancelRuleId : null;

  const selectedRule = selection?.kind === 'rule' ? rules.find(r => r.id === selection.ruleId) : undefined;
  const selectedSkill = selection?.kind === 'skill' ? project.skills.find(s => s.id === selection.skillId) : undefined;
  const isIdleSelected = selection?.kind === 'idle';

  // Smart Timeline binding: when an edge A -> B is selected, show predecessor A's timeline!
  const timelineSkill = useMemo(() => {
    if (selectedSkill) return selectedSkill;
    if (selectedRule) {
      const sourceId = selection?.kind === 'rule' && selection.sourceSkill && selection.sourceSkill !== IDLE_NODE_ID
        ? selection.sourceSkill
        : selectedRule.preSkills[0];
      if (sourceId) return project.skills.find(s => s.id === sourceId);
      return project.skills.find(s => s.id === selectedRule.toSkill);
    }
    return undefined;
  }, [project.skills, selectedSkill, selectedRule, selection]);

  const skillNames = useMemo(() => new Map(project.skills.map(s => [s.id, s.name])), [project.skills]);

  const handleSelectNode = useCallback((id: string) => {
    setActiveCancelRuleId(null);
    setSelection(id === IDLE_NODE_ID ? {kind: 'idle'} : {kind: 'skill', skillId: id});
  }, []);

  const handleSelectEdge = useCallback((edge: ComboDagEdge) => {
    setActiveCancelRuleId(null);
    setSelection({kind: 'rule', ruleId: edge.rule.id, edgeId: edge.id, sourceSkill: edge.source});
  }, []);

  const handleSelectRuleId = useCallback((ruleId: string) => {
    const rule = rules.find(r => r.id === ruleId);
    if (!rule) return;
    const source = rule.preSkills[0] ?? IDLE_NODE_ID;
    setSelection({kind: 'rule', ruleId: rule.id, edgeId: comboEdgeId(rule.id, source), sourceSkill: source});
  }, [rules]);

  const handleConnect = useCallback((sourceId: string, targetSkillId: string) => {
    if (sourceId === IDLE_NODE_ID) {
      let createdId = '';
      update(p => {
        const res = addEntryRule(p, targetSkillId);
        createdId = res.rule.id;
        return res.project;
      });
      if (createdId) {
        setSelection({kind: 'rule', ruleId: createdId, edgeId: comboEdgeId(createdId, IDLE_NODE_ID), sourceSkill: IDLE_NODE_ID});
        report(`已绑定空闲起手 → ${skillNames.get(targetSkillId) ?? targetSkillId}`);
      }
      return;
    }
    let createdId = '';
    update(p => {
      const res = addTransitionRule(p, sourceId, targetSkillId);
      createdId = res.rule.id;
      return res.project;
    });
    if (createdId) {
      setSelection({kind: 'rule', ruleId: createdId, edgeId: comboEdgeId(createdId, sourceId), sourceSkill: sourceId});
      report(`已建立接续 ${skillNames.get(sourceId) ?? sourceId} → ${skillNames.get(targetSkillId) ?? targetSkillId}`);
    }
  }, [update, skillNames, report]);

  const handleDeleteRule = useCallback((ruleId: string) => {
    update(p => deleteComboRule(p, ruleId));
    if (activeCancelRuleId === ruleId) setActiveCancelRuleId(null);
    if (selection?.kind === 'rule' && selection.ruleId === ruleId) setSelection(null);
    report('已删除连招规则（可 Ctrl+Z 撤销）');
  }, [update, activeCancelRuleId, selection, report]);

  const handleDeleteEdge = useCallback((edge: ComboDagEdge) => {
    if (edge.rule.preSkills.length <= 1) {
      handleDeleteRule(edge.rule.id);
    } else {
      update(p => updateComboRule(p, edge.rule.id, {preSkills: edge.rule.preSkills.filter(id => id !== edge.source)}));
      setSelection({kind: 'rule', ruleId: edge.rule.id});
      report(`已移除前置 ${skillNames.get(edge.source) ?? edge.source}`);
    }
  }, [handleDeleteRule, update, skillNames, report]);

  const handleAddEntry = (toSkill?: string) => {
    const target = toSkill ?? project.skills[0]?.id;
    if (!target) return;
    handleConnect(IDLE_NODE_ID, target);
  };

  const handleAddTransition = (fromSkill?: string) => {
    const from = fromSkill ?? selectedSkill?.id ?? visibleSkillIds[0] ?? project.skills[0]?.id;
    if (!from) return;
    const to = project.skills.find(s => s.id !== from)?.id ?? from;
    handleConnect(from, to);
  };

  const handleAddCancel = () => {
    const dodge = project.skills.find(s => s.id.includes('roll') || s.id.includes('dodge')) ?? project.skills[0];
    if (!dodge) return;
    let createdId = '';
    update(p => {
      const pre = visibleSkillIds.filter(id => id !== dodge.id);
      const res = addCancelRule(p, dodge.id, undefined, pre.length ? pre.slice(0, 3) : undefined);
      createdId = res.rule.id;
      return res.project;
    });
    if (createdId) {
      setActiveCancelRuleId(createdId);
      setSelection({kind: 'rule', ruleId: createdId});
      report('已新增公共取消，点击画布技能卡片可切换适用范围');
    }
  };

  const handleAddSkillToCanvas = (skillId: string, pos?: {x: number; y: number}) => {
    setPinnedSkills(prev => prev.includes(skillId) ? prev : [...prev, skillId]);
    if (pos) update(p => saveComboPositions(p, {[skillId]: pos}));
    if (activeChain && !activeChain.skillIds.has(skillId)) setChainFilter('all');
    setSelection({kind: 'skill', skillId});
    setDrawerOpen(false);
  };

  const handleAutoLayout = () => {
    update(p => clearComboPositions(p));
    setFitNonce(n => n + 1);
    report('已自动整理连招画布排版');
  };

  const onCanvasSet = useMemo(() => new Set(visibleSkillIds), [visibleSkillIds]);

  return (
    <section className="combo-editor combo-workbench" aria-label="连招编辑器">
      <div className="combo-topbar">
        <div className="combo-topbar-row">
          <div className="combo-segmented">
            <button type="button" className={surface === 'canvas' ? 'is-active' : ''} onClick={() => setSurface('canvas')}>
              <Workflow size={12}/>连招画布
            </button>
            <button type="button" className={surface === 'table' ? 'is-active' : ''} onClick={() => setSurface('table')}>
              <Table2 size={12}/>全表总览 ({rules.length})
            </button>
          </div>

          {surface === 'canvas' && (
            <div className="combo-chain-pills" role="tablist" aria-label="自动连招链筛选">
              <button type="button" role="tab" aria-selected={chainFilter === 'all'}
                className={chainFilter === 'all' ? 'is-active' : ''}
                onClick={() => { setChainFilter('all'); setFitNonce(n => n + 1); }}>
                全部 ({visibleSkillIds.length && chainFilter === 'all' ? visibleSkillIds.length : project.skills.filter(s => rules.some(r => r.toSkill === s.id || r.preSkills.includes(s.id))).length})
              </button>
              {chains.map(chain => (
                <button key={chain.id} type="button" role="tab" aria-selected={chainFilter === chain.id}
                  className={chainFilter === chain.id ? 'is-active' : ''}
                  onClick={() => { setChainFilter(chain.id); setFitNonce(n => n + 1); }}>
                  {chain.label} ({chain.skillIds.size})
                </button>
              ))}
            </div>
          )}

          <span className="grow"/>

          {surface === 'canvas' && (
            <div className="combo-canvas-actions">
              <div className="popover-anchor">
                <button type="button" className={drawerOpen ? 'is-active' : ''} onClick={() => setDrawerOpen(v => !v)}>
                  <Plus size={12}/>技能库
                </button>
                {drawerOpen && (
                  <ComboSkillDrawer project={project} onCanvasIds={onCanvasSet}
                    onAddSkill={id => handleAddSkillToCanvas(id)} onClose={() => setDrawerOpen(false)}/>
                )}
              </div>
              <button type="button" onClick={handleAutoLayout} title="清除手动位置并按连招主干自动排版">
                <LayoutGrid size={12}/>一键排版
              </button>
              <button type="button" onClick={() => setFitNonce(n => n + 1)} title="适应画布视口">
                <Maximize size={12}/>适应
              </button>
            </div>
          )}
        </div>

        {/* Universal Cancel Bar */}
        <div className="combo-cancel-bar" aria-label="公共取消栏">
          <span className="combo-cancel-bar-label"><Sparkles size={11}/>公共取消</span>
          <div className="combo-cancel-chips">
            {cancelRules.map(rule => {
              const color = inputColor(rule.input);
              const isActive = effectiveActiveCancelId === rule.id;
              return (
                <button key={rule.id} type="button"
                  className={`combo-cancel-chip ${isActive ? 'is-active' : ''}`}
                  style={{'--chip-color': color} as React.CSSProperties}
                  onMouseEnter={() => setHoveredCancelRuleId(rule.id)}
                  onMouseLeave={() => setHoveredCancelRuleId(cur => cur === rule.id ? null : cur)}
                  onClick={() => {
                    if (isActive) {
                      setActiveCancelRuleId(null);
                    } else {
                      setActiveCancelRuleId(rule.id);
                      setSelection({kind: 'rule', ruleId: rule.id});
                    }
                  }}>
                  <i/>
                  <span>{inputShortLabel(project, rule.input)} · {skillNames.get(rule.toSkill) ?? rule.toSkill}</span>
                  <em>{rule.preSkills.length}</em>
                </button>
              );
            })}
            {!cancelRules.length && <small className="combo-cancel-empty">暂无公共取消 · 可将闪避/格挡等全技能打断规则收纳至此</small>}
          </div>
          {cancelRules.length > 0 && surface === 'canvas' && (
            <button type="button" className={`combo-cancel-opt ${showCancelOverlay ? 'is-active' : ''}`}
              onClick={() => setShowCancelOverlay(v => !v)}>
              {showCancelOverlay ? <Eye size={11}/> : <EyeOff size={11}/>} {showCancelOverlay ? '隐藏取消线' : '透视取消线'}
            </button>
          )}
          <button type="button" className="combo-cancel-add" disabled={!project.skills.length} onClick={handleAddCancel}>
            <Plus size={11}/>新增取消
          </button>
        </div>
      </div>

      <div className="combo-main-split">
        <div className="combo-center-column">
          {surface === 'canvas' ? (
            <ComboGraph
              project={project}
              rules={rules}
              layout={layout}
              cancelRuleIds={cancelRuleIds}
              activeCancelRuleId={effectiveActiveCancelId}
              hoveredCancelRuleId={hoveredCancelRuleId}
              store={store}
              selectedNodeId={isIdleSelected ? IDLE_NODE_ID : selectedSkill?.id}
              selectedEdgeId={selection?.kind === 'rule' ? selection.edgeId : undefined}
              fitNonce={fitNonce}
              onSelectNode={handleSelectNode}
              onSelectEdge={handleSelectEdge}
              onOpenSkill={openSkill}
              onConnectNodes={handleConnect}
              onDeleteEdge={handleDeleteEdge}
              onMoveNodes={positions => update(p => saveComboPositions(p, positions), true, 'combo-drag')}
              onDropSkill={(skillId, pos) => handleAddSkillToCanvas(skillId, pos)}
              onToggleCancelSkill={skillId => {
                if (effectiveActiveCancelId) {
                  update(p => toggleCancelRulePreSkill(p, effectiveActiveCancelId, skillId));
                }
              }}
            />
          ) : (
            <ComboRuleTable
              project={project}
              rules={rules}
              cancelRuleIds={cancelRuleIds}
              selectedRuleId={selectedRule?.id}
              onSelectRule={rule => handleSelectRuleId(rule.id)}
              onLocateRule={rule => {
                setChainFilter('all');
                if (cancelRuleIds.has(rule.id)) setShowCancelOverlay(true);
                setSurface('canvas');
                handleSelectRuleId(rule.id);
                setFitNonce(n => n + 1);
              }}
              onDeleteRule={handleDeleteRule}
              onAddEntry={() => handleAddEntry()}
              onAddTransition={() => handleAddTransition()}
              onAddCancel={handleAddCancel}
            />
          )}

          <ComboTimelineDock
            project={project}
            skill={timelineSkill}
            selectedRule={selectedRule}
            rules={rules}
            store={store}
            update={update}
            onSelectRule={handleSelectRuleId}
          />
        </div>

        <ComboInspector
          project={project}
          rules={rules}
          cancelRuleIds={cancelRuleIds}
          selectedRule={selectedRule}
          selectedSkill={selectedSkill}
          isIdleSelected={isIdleSelected}
          onUpdateRule={(ruleId, patch, autoResolve = true) => {
            update(p => updateComboRule(p, ruleId, patch, autoResolve));
            if (patch.id && patch.id !== ruleId && selection?.kind === 'rule' && selection.ruleId === ruleId) {
              setSelection({...selection, ruleId: patch.id});
            }
          }}
          onDeleteRule={handleDeleteRule}
          onToggleCancelBar={ruleId => {
            update(p => toggleCancelBarMembership(p, ruleId));
            if (activeCancelRuleId === ruleId) setActiveCancelRuleId(null);
          }}
          onReorderPeers={orderedIds => update(p => reorderPeerPriorities(p, orderedIds))}
          onSelectRule={handleSelectRuleId}
          onAddEntry={handleAddEntry}
          onAddTransition={handleAddTransition}
          onToggleSkillCancel={(cancelRuleId, skillId) => update(p => toggleCancelRulePreSkill(p, cancelRuleId, skillId))}
          onOpenSkill={openSkill}
          report={report}
        />
      </div>
    </section>
  );
}
