import {BaseEdge, EdgeLabelRenderer} from '@xyflow/react';
import type {Edge, EdgeProps} from '@xyflow/react';
import type {ComboEdgeRoute} from './authoring/comboLayout';

export interface ComboEdgeData extends Record<string, unknown> {
  ruleId: string;
  sourceSkill: string;
  inputLabel: string;
  color: string;
  lane: number;
  laneCount: number;
  route: ComboEdgeRoute;
  badges: {icon: string; text: string; tone: string}[];
  windowText?: string;
  hasConflict?: boolean;
  active?: boolean;
  isCancelOverlay?: boolean;
  onSelect: () => void;
}

export type ComboFlowEdge = Edge<ComboEdgeData, 'comboTransition'>;

export function ComboTransitionEdge({
  id, sourceX, sourceY, targetX, targetY, selected, data, markerEnd,
}: EdgeProps<ComboFlowEdge>) {
  const lane = data?.lane ?? 0;
  const count = data?.laneCount ?? 1;
  const route = data?.route ?? 'forward';
  const color = data?.color ?? '#d9bc78';
  const spread = (lane - (count - 1) / 2) * 38;

  let path = '';
  let labelX = (sourceX + targetX) / 2;
  let labelY = (sourceY + targetY) / 2;

  if (route === 'loop') {
    const lift = 52 + lane * 24;
    const dx = 42 + lane * 10;
    path = `M${sourceX},${sourceY} C${sourceX + dx},${sourceY - lift} ${targetX - dx},${targetY - lift} ${targetX},${targetY}`;
    labelX = (sourceX + targetX) / 2;
    labelY = Math.min(sourceY, targetY) - lift * 0.78;
  } else if (route === 'return') {
    const drop = 56 + lane * 30;
    const bottomY = Math.max(sourceY, targetY) + drop;
    path = `M${sourceX},${sourceY} C${sourceX},${bottomY} ${targetX},${bottomY} ${targetX},${targetY}`;
    labelX = (sourceX + targetX) / 2;
    labelY = bottomY - 12;
  } else {
    const dx = Math.max(54, Math.abs(targetX - sourceX) * 0.45);
    const cy1 = sourceY + spread;
    const cy2 = targetY + spread;
    path = `M${sourceX},${sourceY} C${sourceX + dx},${cy1} ${targetX - dx},${cy2} ${targetX},${targetY}`;
    labelX = (sourceX + targetX) / 2;
    labelY = (sourceY + targetY) / 2 + spread * 0.75;
  }

  const active = !!data?.active;
  const stroke = active ? '#7ce6bd' : selected ? '#f2d99b' : data?.hasConflict ? '#f08775' : color;
  const strokeWidth = active ? 2.8 : selected ? 2.6 : route === 'entry' ? 1.5 : 1.9;
  const dash = route === 'entry' ? '5 4' : data?.isCancelOverlay ? '4 3' : undefined;

  return (
    <>
      <BaseEdge id={id} path={path} markerEnd={markerEnd} interactionWidth={18}
        style={{
          stroke,
          strokeWidth,
          strokeDasharray: dash,
          opacity: data?.isCancelOverlay && !selected && !active ? 0.65 : 0.92,
          filter: active ? 'drop-shadow(0 0 6px rgba(124,230,189,0.55))' : selected ? 'drop-shadow(0 0 5px rgba(217,188,120,0.45))' : undefined,
        }}/>
      <EdgeLabelRenderer>
        <button type="button"
          className={`combo-edge-pill nodrag nopan ${selected ? 'is-selected' : ''} ${active ? 'is-active' : ''} ${data?.hasConflict ? 'is-conflict' : ''}`}
          style={{
            transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
            '--edge-color': color,
          } as React.CSSProperties}
          data-combo-rule-id={data?.ruleId}
          data-combo-source={data?.sourceSkill}
          onClick={event => {
            event.stopPropagation();
            data?.onSelect();
          }}>
          <i className="combo-edge-dot"/>
          <span className="combo-edge-input">{data?.inputLabel}</span>
          {data?.badges.slice(0, 2).map((badge, idx) => (
            <span key={idx} className={`combo-edge-tag tone-${badge.tone}`} title={badge.text}>
              {badge.icon}{badge.text}
            </span>
          ))}
          {(data?.badges.length ?? 0) > 2 && <span className="combo-edge-tag tone-muted">+{(data?.badges.length ?? 0) - 2}</span>}
          {data?.windowText && <span className="combo-edge-tag tone-window">⏱{data.windowText}</span>}
          {data?.hasConflict && <span className="combo-edge-tag tone-conflict" title="同前置同按键优先级冲突">!</span>}
        </button>
      </EdgeLabelRenderer>
    </>
  );
}
