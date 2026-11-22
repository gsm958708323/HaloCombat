import {useCallback, useMemo, useState} from 'react';
import type {Project, RuntimeCombo} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';
import {ComboGraph} from './ComboGraph';
import {ComboRuleForm} from './ComboRuleForm';
import {ComboTimeline} from './ComboTimeline';
import {comboLinkId, layoutCombo} from './authoring/comboLayout';
import type {ComboCard, ComboLink} from './authoring/comboLayout';
import './combo-editor.css';

type Props = {
  project: Project;
  update: (edit: (project: Project) => Project, remember?: boolean, group?: string) => void;
  store: PreviewRuntimeStore;
  report: (message: string) => void;
};

type Selection = {skillId: string; occurrenceId?: string; ruleId?: string; edgeId?: string};
const cardSelection = ({id, skillId}: ComboCard): Selection => ({occurrenceId: id, skillId});
const emptyRules: RuntimeCombo[] = [];

export function ComboEditor({project, update, store, report}: Props) {
  const rules = project.runtime?.combos ?? emptyRules;
  const layout = useMemo(() => layoutCombo(rules), [rules]);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [creation, setCreation] = useState<RuntimeCombo | null>(null);
  const edge = selection?.edgeId ? layout.linksById.get(selection.edgeId) : undefined;
  const card = edge ? layout.cards.get(edge.targetId) : selection ?
    layout.cards.get(selection.occurrenceId ?? '') ?? layout.mainCards.get(selection.skillId) : undefined;
  const skill = card && project.skills.find(item => item.id === card.skillId);
  const related = useMemo(() => {
    if (!skill) return [];
    const incoming = rules.filter(rule => rule.preSkills.length && rule.toSkill === skill.id);
    const outgoing = rules.filter(rule => rule.toSkill !== skill.id && rule.preSkills.includes(skill.id));
    return [...incoming, ...outgoing];
  }, [rules, skill]);
  const localIncoming = card && layout.edges.find(link => link.targetId === card.id && link.sourceId !== card.id);
  const preferredRuleId = edge?.rule.id ?? selection?.ruleId ?? localIncoming?.rule.id;
  const row = related.find(item => item.id === preferredRuleId) ?? related[0];

  const selectSkill = useCallback((occurrenceId: string) => {
    const clicked = layout.cards.get(occurrenceId);
    if (!clicked) return;
    setSelection(cardSelection(clicked));
    setCreation(null);
  }, [layout]);
  const selectEdge = useCallback((link: ComboLink) => {
    setSelection({skillId: link.target, occurrenceId: link.targetId, ruleId: link.rule.id, edgeId: link.id});
    setCreation(null);
  }, []);

  function addRule() {
    const source = skill ?? project.skills.find(item => item.id === layout.roots[0]?.skillId) ?? project.skills[0];
    if (!source) return;
    const target = project.skills.find(item => item.id !== source.id) ?? source;
    setCreation({id: crypto.randomUUID(), purpose: 'transition', preSkills: [source.id],
      input: row?.input ?? 'Attack', requiredTags: [], priority: 0, toSkill: target.id});
  }

  function committed(previous: RuntimeCombo | undefined, next: RuntimeCombo | null) {
    setCreation(null);
    if (!next?.preSkills.length) {
      setSelection(card ? cardSelection(card) : null);
      return;
    }
    if (!previous || edge) {
      const source = edge && next.preSkills.includes(edge.source) ? edge.source : next.preSkills[0];
      setSelection({skillId: next.toSkill, ruleId: next.id, edgeId: comboLinkId(next.id, source),
        occurrenceId: card?.skillId === next.toSkill ? card.id : undefined});
      return;
    }
    if (card && (next.toSkill === card.skillId || next.preSkills.includes(card.skillId))) {
      setSelection({...cardSelection(card), ruleId: next.id});
    } else {
      setSelection({skillId: next.toSkill, ruleId: next.id, edgeId: comboLinkId(next.id, next.preSkills[0])});
    }
  }

  return <section className="combo-editor" aria-label="连招编辑器">
    <div className="combo-workspace">
      <ComboGraph project={project} layout={layout} store={store} occurrenceId={card?.id} edgeId={edge?.id}
        selectSkill={selectSkill} selectEdge={selectEdge}/>
      <aside className="combo-edit-inspector" aria-label="选中技能与连招配置">
        {skill ? <ComboTimeline key={skill.id} project={project} skill={skill} store={store} update={update}/>
          : <p className="combo-inspector-hint">选择技能或接续线查看 Timeline 和规则。</p>}
        <section className="combo-rule-section" aria-label="连招配置">
          <header><h3>连招配置</h3><button type="button" disabled={!!creation || !project.skills.length} onClick={addRule}>新增接续</button></header>
          {creation ? <p>新增接续 · 编辑前置和目标技能 ID</p> : row && <label className="combo-related">
            关联规则<select value={row.id} onChange={event => card && setSelection({...cardSelection(card), ruleId: event.target.value})}>
              {related.map(item => <option key={item.id} value={item.id}>
                {item.toSkill === skill?.id ? '入边' : '出边'} · {item.input} · {item.id}
              </option>)}
            </select>
          </label>}
          {(creation || row) && <ComboRuleForm key={JSON.stringify(creation ? ['new', creation.id] : [card?.id, edge?.id, row!.id])}
            project={project} row={creation ? undefined : row} initial={creation ?? row!} update={update}
            committed={committed} cancelNew={() => setCreation(null)} report={report}/>}
        </section>
      </aside>
    </div>
  </section>;
}
