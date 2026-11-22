import {useCallback, useMemo, useSyncExternalStore} from 'react';
import {Background, BackgroundVariant, Handle, MarkerType, Position, ReactFlow, ReactFlowProvider, useReactFlow} from '@xyflow/react';
import type {Edge, Node, NodeProps} from '@xyflow/react';
import type {Project} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';
import type {ComboLink, layoutCombo} from './authoring/comboLayout';
import {ComboTransitionEdge} from './ComboTransitionEdge';
import {createActiveComboCard} from './authoring/comboActiveCard';

const rootId = 'guide:root';
const realEdgeId = (id: string) => 'rule:' + id;
const minZoom = .05;
const fitOptions = {padding: .2, minZoom, maxZoom: 1};
const nodeOrigin: [number, number] = [0, 0];

type ComboNode = Node<{skillId?: string; name: string; active: boolean; reference?: boolean;
  select: (occurrenceId: string) => void}, 'combo'>;

function ComboSkillNode({id, data, selected}: NodeProps<ComboNode>) {
  if (data.skillId === undefined) return <div className="combo-skill-node combo-root" data-combo-root="true">
    <strong>连招总览</strong><small>各接续链入口</small><Handle type="source" position={Position.Right}/>
  </div>;
  const skillId = data.skillId;
  return <div className="combo-node-shell">
    <Handle id="forward-in" type="target" position={Position.Left}/>
    <button type="button" className="combo-skill-node nodrag" data-skill-id={skillId} data-combo-active={data.active}
      data-combo-occurrence={id} data-combo-reference={!!data.reference}
      aria-pressed={selected} aria-label={`${data.name} (${skillId})${data.reference ? ' · 引用' : ''}${data.active ? ' · 正在执行' : ''}`}
      onClick={() => data.select(id)}>
      <strong title={data.name}>{data.name}</strong>
      <small title={skillId}>{data.active ? '正在执行' : skillId}</small>
      {data.reference && <span className="combo-reference-label">引用</span>}
    </button>
    {!data.reference && <>
      <Handle id="forward-out" type="source" position={Position.Right}/>
      <Handle id="return-out" type="source" position={Position.Left} className="combo-routing-handle combo-return-handle"/>
      <Handle id="return-in" type="target" position={Position.Right} className="combo-routing-handle combo-return-handle"/>
      <Handle id="loop-out" type="source" position={Position.Top} className="combo-routing-handle combo-loop-out"/>
      <Handle id="loop-in" type="target" position={Position.Top} className="combo-routing-handle combo-loop-in"/>
    </>}
  </div>;
}

const nodeTypes = {combo: ComboSkillNode};
const edgeTypes = {transition: ComboTransitionEdge};

type Props = {
  project: Project;
  layout: ReturnType<typeof layoutCombo>;
  store: PreviewRuntimeStore;
  occurrenceId?: string;
  edgeId?: string;
  selectSkill: (id: string) => void;
  selectEdge: (edge: ComboLink) => void;
};

function Canvas({project, layout, store, occurrenceId, edgeId, selectSkill, selectEdge}: Props) {
  const flow = useReactFlow<ComboNode>();
  const activeCard = useMemo(() => createActiveComboCard(layout), [layout]);
  const getActive = useCallback(() => activeCard(store.get().state), [activeCard, store]);
  const active = useSyncExternalStore(store.subscribe, getActive);
  const nodes = useMemo<ComboNode[]>(() => {
    const names = new Map(project.skills.map(skill => [skill.id, skill.name]));
    return [{id: rootId, type: 'combo', position: {x: 0, y: 0}, selectable: false, focusable: false,
      data: {name: '连招总览', active: false, select: selectSkill}},
    ...Array.from(layout.cards.values(), ({id, skillId, position, reference}): ComboNode => ({
      id, type: 'combo', position, selected: id === occurrenceId, focusable: false,
      data: {skillId, name: names.get(skillId) ?? skillId, active: id === active, reference, select: selectSkill},
    }))];
  }, [project.skills, layout, occurrenceId, active, selectSkill]);
  const edges = useMemo<Edge[]>(() => {
    const guides = layout.roots.map(({id}): Edge => ({id: 'guide-edge:' + id, source: rootId, target: id,
      targetHandle: 'forward-in', type: 'default', selectable: false, focusable: false, className: 'combo-guide-edge'}));
    const rules = layout.edges.map((link): Edge => {
      const selected = link.id === edgeId;
      const from = layout.cards.get(link.sourceId)!;
      const to = layout.cards.get(link.targetId)!;
      const route = from.id === to.id ? 'loop' : from.position.x > to.position.x ? 'return' : 'forward';
      return {
        id: realEdgeId(link.id), source: link.sourceId, target: link.targetId, type: 'transition', selected,
        sourceHandle: `${route}-out`, targetHandle: `${route}-in`,
        markerEnd: {type: MarkerType.ArrowClosed, color: selected ? '#ffda8f' : '#a3b9bf', width: 15, height: 15},
        label: link.rule.input, ariaLabel: `${link.source} → ${link.target} · ${link.rule.input} · ${link.rule.id}`,
        domAttributes: {'data-combo-rule-id': link.rule.id, 'data-combo-source': link.source,
          'data-combo-source-occurrence': link.sourceId, 'data-combo-target-occurrence': link.targetId, onKeyDown: event => {
          if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            event.stopPropagation();
            selectEdge(link);
          }
        }} as Edge['domAttributes'],
        data: {lane: link.lane, laneCount: link.laneCount, route},
      };
    });
    return [...guides, ...rules];
  }, [layout, edgeId, selectEdge]);
  const edgeById = useMemo(() => new Map(layout.edges.map(edge => [realEdgeId(edge.id), edge])), [layout]);

  return <div className="combo-canvas">
    <div className="combo-graph-tools">
      <span>{layout.ruleCount} 条规则 · {layout.mainCards.size} 个技能 · {layout.cards.size} 张卡片 · {layout.edges.length} 条接续线</span>
      <button type="button" onClick={() => void flow.fitView({...fitOptions, duration: 180})}>适应画布</button>
    </div>
    <ReactFlow nodes={nodes} edges={edges} nodeTypes={nodeTypes} edgeTypes={edgeTypes} nodeOrigin={nodeOrigin}
      aria-label="全项目连招图" fitView fitViewOptions={fitOptions} minZoom={minZoom} maxZoom={2}
      nodesDraggable={false} nodesConnectable={false} deleteKeyCode={null} selectionKeyCode={null} panOnDrag
      onEdgeClick={(_, edge) => { const link = edgeById.get(edge.id); if (link) selectEdge(link); }}>
      <Background variant={BackgroundVariant.Dots} gap={22} size={1.2} color="#35454c"/>
    </ReactFlow>
    {!layout.edges.length && <p className="combo-empty-graph">暂无接续规则</p>}
  </div>;
}

export function ComboGraph(props: Props) {
  return <ReactFlowProvider><Canvas {...props}/></ReactFlowProvider>;
}
