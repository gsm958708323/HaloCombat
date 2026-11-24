import {memo} from 'react';
import {Handle, Position} from '@xyflow/react';
import type {Node, NodeProps} from '@xyflow/react';
import {Plus, Sparkles, Check} from 'lucide-react';
import {channelColor, IDLE_NODE_ID} from './authoring/comboModel';
import type {SkillComboMeta} from './authoring/comboModel';

export interface CancelDotInfo {
  ruleId: string;
  label: string;
  color: string;
}

export interface ComboSkillNodeData extends Record<string, unknown> {
  isIdle?: boolean;
  entryCount?: number;
  skillId?: string;
  name: string;
  meta?: SkillComboMeta;
  active: boolean;
  playProgress: number | null;
  cancelDots?: CancelDotInfo[];
  cancelInspectMode?: boolean;
  cancelMember?: boolean;
  cancelHoverHighlighted?: boolean;
  cancelHoverDimmed?: boolean;
  onSelect: (id: string) => void;
  onOpenSkill: (skillId: string) => void;
  onQuickAppend: (sourceId: string, anchorRect?: DOMRect) => void;
  onToggleCancelSkill?: (skillId: string) => void;
}

export type ComboFlowNode = Node<ComboSkillNodeData, 'comboSkill'>;

export const ComboSkillNode = memo(function ComboSkillNode({id, data, selected}: NodeProps<ComboFlowNode>) {
  if (data.isIdle || id === IDLE_NODE_ID) {
    return (
      <div className={`combo-idle-node ${selected ? 'is-selected' : ''}`} data-combo-idle="true"
        onClick={() => data.onSelect(IDLE_NODE_ID)}>
        <div className="combo-idle-head">
          <Sparkles size={13} aria-hidden="true"/>
          <strong>空闲起手</strong>
          <span className="combo-idle-count">{data.entryCount ?? 0}</span>
        </div>
        <small>拖线绑定或点击 + 添加起手技</small>
        <button type="button" className="combo-node-plus nodrag nopan" title="新增空闲起手绑定"
          aria-label="新增空闲起手绑定"
          onClick={event => {
            event.stopPropagation();
            data.onQuickAppend(IDLE_NODE_ID, event.currentTarget.getBoundingClientRect());
          }}>
          <Plus size={12}/>
        </button>
        <Handle id="forward-out" type="source" position={Position.Right} className="combo-handle combo-handle-out"/>
      </div>
    );
  }

  const skillId = data.skillId!;
  const meta = data.meta!;
  const duration = meta.duration;
  const durationLabel = meta.timelineKind === 'operation' ? '即时' : `${duration.toFixed(2)}s`;

  const classes = [
    'combo-card',
    selected ? 'is-selected' : '',
    data.active ? 'is-active' : '',
    data.cancelHoverHighlighted ? 'is-cancel-hit' : '',
    data.cancelHoverDimmed ? 'is-cancel-dim' : '',
    data.cancelInspectMode ? (data.cancelMember ? 'is-cancel-member' : 'is-cancel-candidate') : '',
  ].filter(Boolean).join(' ');

  return (
    <div className={classes} data-skill-id={skillId} data-combo-active={data.active}
      style={{'--skill-accent': meta.accent} as React.CSSProperties}
      onClick={event => {
        event.stopPropagation();
        if (data.cancelInspectMode && data.onToggleCancelSkill) {
          data.onToggleCancelSkill(skillId);
        } else {
          data.onSelect(skillId);
        }
      }}
      onDoubleClick={event => {
        event.stopPropagation();
        if (!data.cancelInspectMode) data.onOpenSkill(skillId);
      }}>
      <div className="combo-card-accent"/>
      <Handle id="forward-in" type="target" position={Position.Left} className="combo-handle combo-handle-in"/>
      <Handle id="forward-out" type="source" position={Position.Right} className="combo-handle combo-handle-out"/>
      <Handle id="return-out" type="source" position={Position.Bottom} className="combo-routing-handle combo-return-out"/>
      <Handle id="return-in" type="target" position={Position.Bottom} className="combo-routing-handle combo-return-in"/>
      <Handle id="loop-out" type="source" position={Position.Top} className="combo-routing-handle combo-loop-out"/>
      <Handle id="loop-in" type="target" position={Position.Top} className="combo-routing-handle combo-loop-in"/>

      <div className="combo-card-body">
        <div className="combo-card-top">
          <strong className="combo-card-title" title={`${data.name} (${skillId})`}>{data.name}</strong>
          <span className="combo-card-duration">{durationLabel}</span>
        </div>
        <div className="combo-card-sub">
          <span className="combo-card-type">{meta.typeLabel}</span>
          {data.cancelInspectMode ? (
            <span className={`combo-cancel-toggle-pill ${data.cancelMember ? 'on' : ''}`}>
              {data.cancelMember ? <><Check size={10}/> 可取消</> : '+ 纳入'}
            </span>
          ) : (
            !!data.cancelDots?.length && (
              <span className="combo-card-cancel-dots" title={`可被公共取消：${data.cancelDots.map(d => d.label).join('、')}`}>
                {data.cancelDots.slice(0, 4).map(dot => (
                  <i key={dot.ruleId} style={{background: dot.color}}/>
                ))}
              </span>
            )
          )}
        </div>
      </div>

      <div className="combo-card-bar" aria-hidden="true">
        {duration > 0 && meta.cancelSegments.map((seg, idx) => {
          const left = Math.max(0, Math.min(100, (seg.start / duration) * 100));
          const width = Math.max(2, Math.min(100 - left, ((seg.end - seg.start) / duration) * 100));
          return (
            <span key={idx} className="combo-card-bar-seg"
              style={{left: `${left}%`, width: `${width}%`, background: channelColor(seg.channel)}}/>
          );
        })}
        {data.active && data.playProgress !== null && (
          <span className="combo-card-bar-live" style={{width: `${Math.max(2, Math.min(100, data.playProgress * 100))}%`}}/>
        )}
      </div>

      {!data.cancelInspectMode && (
        <button type="button" className="combo-node-plus nodrag nopan" title="快捷接下一段技能"
          aria-label={`为 ${data.name} 添加下一段接续`}
          onClick={event => {
            event.stopPropagation();
            data.onQuickAppend(skillId, event.currentTarget.getBoundingClientRect());
          }}>
          <Plus size={12}/>
        </button>
      )}
    </div>
  );
});
