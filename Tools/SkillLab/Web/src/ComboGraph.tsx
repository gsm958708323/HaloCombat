import {useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore} from 'react';
import {Background, BackgroundVariant, MarkerType, MiniMap, ReactFlow, ReactFlowProvider, useReactFlow} from '@xyflow/react';
import type {Connection, NodeChange} from '@xyflow/react';
import {Search} from 'lucide-react';
import type {Project, RuntimeCombo} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';
import type {ComboDagEdge, ComboDagLayout} from './authoring/comboLayout';
import {createActiveComboTracker} from './authoring/comboActiveCard';
import {
  conditionBadge, IDLE_NODE_ID, inputColor, inputShortLabel,
  priorityConflict, skillMeta, windowBadge,
} from './authoring/comboModel';
import {ComboSkillNode} from './ComboSkillNode';
import type {CancelDotInfo, ComboFlowNode} from './ComboSkillNode';
import {ComboTransitionEdge} from './ComboTransitionEdge';
import type {ComboFlowEdge} from './ComboTransitionEdge';

const nodeTypes = {comboSkill: ComboSkillNode};
const edgeTypes = {comboTransition: ComboTransitionEdge};
const minZoom = 0.15;
const fitOptions = {padding: 0.18, minZoom, maxZoom: 1.15};

type Props = {
  project: Project;
  rules: readonly RuntimeCombo[];
  layout: ComboDagLayout;
  cancelRuleIds: ReadonlySet<string>;
  activeCancelRuleId: string | null;
  hoveredCancelRuleId: string | null;
  store: PreviewRuntimeStore;
  selectedNodeId?: string;
  selectedEdgeId?: string;
  fitNonce: number;
  onSelectNode: (id: string) => void;
  onSelectEdge: (edge: ComboDagEdge) => void;
  onOpenSkill: (skillId: string) => void;
  onConnectNodes: (sourceId: string, targetSkillId: string) => void;
  onDeleteEdge: (edge: ComboDagEdge) => void;
  onMoveNodes: (positions: Record<string, {x: number; y: number}>) => void;
  onDropSkill: (skillId: string, position: {x: number; y: number}) => void;
  onToggleCancelSkill: (skillId: string) => void;
};

function Canvas(props: Props) {
  const {
    project, rules, layout, cancelRuleIds, activeCancelRuleId, hoveredCancelRuleId,
    store, selectedNodeId, selectedEdgeId, fitNonce,
    onSelectNode, onSelectEdge, onOpenSkill, onConnectNodes, onDeleteEdge,
    onMoveNodes, onDropSkill, onToggleCancelSkill,
  } = props;

  const flow = useReactFlow<ComboFlowNode, ComboFlowEdge>();
  const tracker = useMemo(() => createActiveComboTracker(), []);
  const getHighlight = useCallback(() => tracker(store.get().state), [tracker, store]);
  const highlight = useSyncExternalStore(store.subscribe, getHighlight);

  const [measurements, setMeasurements] = useState<Record<string, {width: number; height: number}>>({});
  const [dragPositions, setDragPositions] = useState<Map<string, {x: number; y: number}>>(new Map());
  const [draggingIds, setDraggingIds] = useState<Set<string>>(new Set());
  const [appendSource, setAppendSource] = useState<{sourceId: string; x: number; y: number} | null>(null);
  const [appendQuery, setAppendQuery] = useState('');
  const wrapperRef = useRef<HTMLDivElement>(null);

  const initializedRef = useRef(false);
  useEffect(() => {
    if (initializedRef.current) return;
    initializedRef.current = true;
    const timer = window.setTimeout(() => void flow.fitView({...fitOptions, duration: 160}), 80);
    return () => window.clearTimeout(timer);
  }, [flow]);

  useEffect(() => {
    if (fitNonce <= 0) return;
    const frame = requestAnimationFrame(() => void flow.fitView({...fitOptions, duration: 200}));
    return () => cancelAnimationFrame(frame);
  }, [fitNonce, flow]);

  const openQuickAppend = useCallback((sourceId: string, anchorRect?: DOMRect) => {
    const container = wrapperRef.current?.getBoundingClientRect();
    const x = anchorRect && container ? Math.min(container.width - 235, Math.max(12, anchorRect.right - container.left + 8)) : 180;
    const y = anchorRect && container ? Math.min(container.height - 250, Math.max(12, anchorRect.top - container.top - 10)) : 80;
    setAppendQuery('');
    setAppendSource({sourceId, x, y});
  }, []);

  const activeCancelRule = useMemo(
    () => (activeCancelRuleId ? rules.find(r => r.id === activeCancelRuleId) : undefined),
    [rules, activeCancelRuleId],
  );
  const hoveredCancelRule = useMemo(
    () => (hoveredCancelRuleId ? rules.find(r => r.id === hoveredCancelRuleId) : undefined),
    [rules, hoveredCancelRuleId],
  );

  const cancelDotsBySkill = useMemo(() => {
    const map = new Map<string, CancelDotInfo[]>();
    const names = new Map(project.skills.map(s => [s.id, s.name]));
    for (const rule of rules) {
      if (!cancelRuleIds.has(rule.id)) continue;
      const color = inputColor(rule.input);
      const label = `${inputShortLabel(project, rule.input)} · ${names.get(rule.toSkill) ?? rule.toSkill}`;
      for (const pre of rule.preSkills) {
        let list = map.get(pre);
        if (!list) map.set(pre, list = []);
        list.push({ruleId: rule.id, label, color});
      }
    }
    return map;
  }, [project, rules, cancelRuleIds]);

  const entryCount = useMemo(() => rules.filter(r => !r.preSkills.length).length, [rules]);

  const nodes = useMemo<ComboFlowNode[]>(() => {
    const skillMap = new Map(project.skills.map(s => [s.id, s]));
    const idlePos = dragPositions.get(IDLE_NODE_ID) ?? layout.positions.get(IDLE_NODE_ID) ?? {x: 0, y: 0};
    const result: ComboFlowNode[] = [{
      id: IDLE_NODE_ID,
      type: 'comboSkill',
      position: idlePos,
      measured: measurements[IDLE_NODE_ID],
      dragging: draggingIds.has(IDLE_NODE_ID),
      selected: selectedNodeId === IDLE_NODE_ID,
      data: {
        isIdle: true,
        entryCount,
        name: '空闲起手',
        active: false,
        playProgress: null,
        onSelect: onSelectNode,
        onOpenSkill,
        onQuickAppend: openQuickAppend,
      },
    }];

    for (const skillId of layout.skillIds) {
      const skill = skillMap.get(skillId);
      if (!skill) continue;
      const meta = skillMeta(project, skill);
      const isActive = highlight.skillId === skillId;
      const dur = highlight.actionDuration ?? meta.duration;
      const progress = isActive && highlight.actionTime !== null && dur > 0 ? highlight.actionTime / dur : null;
      const pos = dragPositions.get(skillId) ?? layout.positions.get(skillId) ?? {x: 310, y: 0};
      const hoverHit = hoveredCancelRule ? hoveredCancelRule.preSkills.includes(skillId) : false;
      const hoverDim = !!hoveredCancelRule && !hoverHit;

      result.push({
        id: skillId,
        type: 'comboSkill',
        position: pos,
        measured: measurements[skillId],
        dragging: draggingIds.has(skillId),
        selected: selectedNodeId === skillId,
        data: {
          skillId,
          name: skill.name,
          meta,
          active: isActive,
          playProgress: progress,
          cancelDots: cancelDotsBySkill.get(skillId),
          cancelInspectMode: !!activeCancelRule,
          cancelMember: activeCancelRule ? activeCancelRule.preSkills.includes(skillId) : false,
          cancelHoverHighlighted: hoverHit,
          cancelHoverDimmed: hoverDim,
          onSelect: onSelectNode,
          onOpenSkill,
          onQuickAppend: openQuickAppend,
          onToggleCancelSkill,
        },
      });
    }
    return result;
  }, [
    project, layout, measurements, dragPositions, draggingIds, selectedNodeId, entryCount, highlight,
    cancelDotsBySkill, activeCancelRule, hoveredCancelRule,
    onSelectNode, onOpenSkill, openQuickAppend, onToggleCancelSkill,
  ]);

  const edges = useMemo<ComboFlowEdge[]>(() => layout.edges.map(link => {
    const selected = link.id === selectedEdgeId;
    const active = highlight.edgeId === link.id;
    const color = inputColor(link.rule.input);
    const conflict = !!priorityConflict(rules, link.rule);
    const badges = (link.rule.conditions ?? []).map(c => conditionBadge(c, project));
    const win = link.source === IDLE_NODE_ID ? null : windowBadge(link.rule.window);
    const strokeColor = active ? '#7ce6bd' : selected ? '#f2d99b' : conflict ? '#f08775' : color;
    return {
      id: link.id,
      source: link.source,
      target: link.target,
      type: 'comboTransition',
      selected,
      sourceHandle: link.route === 'loop' ? 'loop-out' : link.route === 'return' ? 'return-out' : 'forward-out',
      targetHandle: link.route === 'loop' ? 'loop-in' : link.route === 'return' ? 'return-in' : 'forward-in',
      markerEnd: {type: MarkerType.ArrowClosed, color: strokeColor, width: 15, height: 15},
      data: {
        ruleId: link.rule.id,
        sourceSkill: link.source,
        inputLabel: inputShortLabel(project, link.rule.input),
        color,
        lane: link.lane,
        laneCount: link.laneCount,
        route: link.route,
        badges,
        windowText: win?.text,
        hasConflict: conflict,
        active,
        isCancelOverlay: link.isCancelOverlay,
        onSelect: () => onSelectEdge(link),
      },
    };
  }), [layout, selectedEdgeId, highlight.edgeId, rules, project, onSelectEdge]);

  const handleNodesChange = useCallback((changes: NodeChange<ComboFlowNode>[]) => {
    const dimensions = changes.filter(c => c.type === 'dimensions');
    if (dimensions.length) {
      setMeasurements(prev => {
        let next = prev;
        for (const item of dimensions) {
          if (item.type !== 'dimensions' || !item.dimensions) continue;
          const old = prev[item.id];
          if (!old || old.width !== item.dimensions.width || old.height !== item.dimensions.height) {
            if (next === prev) next = {...prev};
            next[item.id] = item.dimensions;
          }
        }
        return next;
      });
    }

    const posChanges = changes.filter(c => c.type === 'position');
    if (!posChanges.length) return;

    const moving = posChanges.filter(c => c.type === 'position' && c.position);
    if (moving.length) {
      setDragPositions(prev => {
        const next = new Map(prev);
        for (const item of moving) {
          if (item.type === 'position' && item.position) next.set(item.id, item.position);
        }
        return next;
      });
    }

    const hasDragFlag = posChanges.some(c => c.type === 'position' && typeof c.dragging === 'boolean');
    if (hasDragFlag) {
      setDraggingIds(prev => {
        let next = prev;
        for (const item of posChanges) {
          if (item.type !== 'position' || typeof item.dragging !== 'boolean') continue;
          if (item.dragging && !next.has(item.id)) {
            if (next === prev) next = new Set(prev);
            next.add(item.id);
          } else if (!item.dragging && next.has(item.id)) {
            if (next === prev) next = new Set(prev);
            next.delete(item.id);
          }
        }
        return next;
      });
    }
  }, []);

  const handleNodeDragStop = useCallback((_: unknown, node: ComboFlowNode, allDragged: ComboFlowNode[]) => {
    const updates: Record<string, {x: number; y: number}> = {};
    const list = allDragged.length ? allDragged : [node];
    for (const item of list) updates[item.id] = {x: item.position.x, y: item.position.y};
    onMoveNodes(updates);
    setDragPositions(new Map());
    setDraggingIds(new Set());
  }, [onMoveNodes]);

  const handleConnect = useCallback((connection: Connection) => {
    if (!connection.source || !connection.target || connection.target === IDLE_NODE_ID) return;
    onConnectNodes(connection.source, connection.target);
  }, [onConnectNodes]);

  const appendCandidates = useMemo(() => {
    const q = appendQuery.trim().toLowerCase();
    return project.skills.filter(s => !q || s.name.toLowerCase().includes(q) || s.id.toLowerCase().includes(q));
  }, [project.skills, appendQuery]);

  return (
    <div ref={wrapperRef} className="combo-canvas-stage" tabIndex={0}
      onKeyDown={event => {
        if ((event.target as HTMLElement).closest('input,textarea,select')) return;
        if ((event.key === 'Delete' || event.key === 'Backspace') && selectedEdgeId) {
          const edge = layout.edgesById.get(selectedEdgeId);
          if (edge) {
            event.preventDefault();
            onDeleteEdge(edge);
          }
        }
        if (event.key === 'Escape') setAppendSource(null);
      }}
      onDragOver={event => {
        if (event.dataTransfer.types.includes('application/x-halocombat-skill')) {
          event.preventDefault();
          event.dataTransfer.dropEffect = 'copy';
        }
      }}
      onDrop={event => {
        const skillId = event.dataTransfer.getData('application/x-halocombat-skill');
        if (!skillId) return;
        event.preventDefault();
        const pos = flow.screenToFlowPosition({x: event.clientX, y: event.clientY});
        onDropSkill(skillId, pos);
      }}>
      <ReactFlow<ComboFlowNode, ComboFlowEdge>
        nodes={nodes}
        edges={edges}
        nodeTypes={nodeTypes}
        edgeTypes={edgeTypes}
        onNodesChange={handleNodesChange}
        onNodeDragStop={handleNodeDragStop}
        onConnect={handleConnect}
        onPaneClick={() => setAppendSource(null)}
        onEdgeClick={(_, edge) => {
          const found = layout.edgesById.get(edge.id);
          if (found) onSelectEdge(found);
        }}
        fitView
        fitViewOptions={fitOptions}
        minZoom={minZoom}
        maxZoom={2}
        nodesDraggable={!activeCancelRule}
        nodesConnectable={!activeCancelRule}
        deleteKeyCode={null}
        colorMode="dark"
        proOptions={{hideAttribution: true}}>
        <Background variant={BackgroundVariant.Dots} gap={22} size={1.1} color="#2c3c43"/>
        <MiniMap pannable zoomable
          nodeColor={node => node.id === IDLE_NODE_ID ? '#80a6b8' : node.data?.active ? '#7ce6bd' : node.selected ? '#e5c982' : '#4f6d79'}
          maskColor="rgba(12, 19, 22, 0.72)"/>
      </ReactFlow>

      {activeCancelRule && (
        <div className="combo-cancel-banner" role="status">
          <span>正在配置公共取消 <strong>{inputShortLabel(project, activeCancelRule.input)} → {project.skills.find(s => s.id === activeCancelRule.toSkill)?.name ?? activeCancelRule.toSkill}</strong>：点击任意技能卡片即可勾选或取消适用范围</span>
        </div>
      )}

      {appendSource && (
        <div className="combo-quick-append" style={{left: appendSource.x, top: appendSource.y}} role="dialog" aria-label="快捷连接技能">
          <div className="combo-drawer-search">
            <Search size={12}/>
            <input autoFocus value={appendQuery} placeholder={appendSource.sourceId === IDLE_NODE_ID ? '选择起手技能…' : '选择下一段技能…'}
              onChange={e => setAppendQuery(e.target.value)}/>
          </div>
          <div className="combo-quick-append-list">
            {appendCandidates.map(skill => (
              <button key={skill.id} type="button" onClick={() => {
                onConnectNodes(appendSource.sourceId, skill.id);
                setAppendSource(null);
              }}>
                <span className="combo-drawer-swatch" style={{background: skillMeta(project, skill).accent}}/>
                <span>{skill.name}</span>
                <small>{skill.id}</small>
              </button>
            ))}
            {!appendCandidates.length && <p className="combo-drawer-empty">无匹配技能</p>}
          </div>
        </div>
      )}
    </div>
  );
}

export function ComboGraph(props: Props) {
  return <ReactFlowProvider><Canvas {...props}/></ReactFlowProvider>;
}
