import {Handle, Position, useUpdateNodeInternals} from '@xyflow/react';
import type {Node, NodeProps} from '@xyflow/react';
import {Activity, Box, Crosshair, Flame, GitBranch, HeartPulse, Layers, Radio, Send, Sparkles, Split, Waves, X, Zap} from 'lucide-react';
import {useEffect} from 'react';
import type {NodeData} from './NodeParameters';
import {NodeParameters} from './NodeParameters';
import {nodePortGroups} from './authoring/nodePresentation';
import {kinds, portName} from './model';

const nodeVisuals = {
  target: {tone: 'input', icon: Crosshair},
  cast: {tone: 'input', icon: Zap},
  projectile: {tone: 'delivery', icon: Send},
  'projectile-ref': {tone: 'delivery', icon: Send},
  delivery: {tone: 'delivery', icon: Waves},
  'area-ref': {tone: 'delivery', icon: Waves},
  construct: {tone: 'delivery', icon: Box},
  'after-damage': {tone: 'condition', icon: GitBranch},
  'skill-events': {tone: 'event', icon: Radio},
  'buff-events': {tone: 'event', icon: Radio},
  cue: {tone: 'event', icon: Sparkles},
  'hurt-feedback': {tone: 'event', icon: Activity},
  damage: {tone: 'effect', icon: Activity},
  split: {tone: 'effect', icon: Split},
  chain: {tone: 'effect', icon: GitBranch},
  pierce: {tone: 'effect', icon: Send},
  impact: {tone: 'effect', icon: Zap},
  buff: {tone: 'effect', icon: Layers},
  dispel: {tone: 'effect', icon: Layers},
  heal: {tone: 'effect', icon: HeartPulse},
  'refill-ammo': {tone: 'effect', icon: Box},
  'teleport-projectile': {tone: 'effect', icon: Crosshair},
  'construct-chain': {tone: 'effect', icon: Flame},
} as const;

const toneNames = {input: '施放', delivery: '传递', condition: '条件', event: '事件', effect: '效果'};

type NodeVisual = typeof nodeVisuals[keyof typeof nodeVisuals];
export type EditorNode = Node<NodeData & Record<string, unknown>, 'skill'>;

export function SkillNode({data, selected}: NodeProps<EditorNode>) {
  const schema = kinds[data.kind];
  const update = useUpdateNodeInternals();
  const ports = nodePortGroups(data, data.counts);
  const portKey = ports.main.map(([key]) => key).join(',') + '|' + ports.extra.map(([key]) => key).join(',');
  useEffect(() => update(data.id), [data.id, portKey, update]);
  if (!schema) return <div className="skill-node">未知节点：{data.kind}</div>;

  const visual: NodeVisual = nodeVisuals[data.kind as keyof typeof nodeVisuals] ?? {tone: 'effect', icon: Layers};
  const Icon = visual.icon;
  const title = schema.title;
  const updatePorts = () => requestAnimationFrame(() => update(data.id));

  function output([port, label]: [string, string]) {
    const count = data.counts?.[port] ?? 0;
    return <div className="port-row output" key={port} data-connected={count > 0}>
      <span>{label} <small aria-label={`${label}连接数`}>{count}</small></span>
      <Handle type="source" id={port} position={Position.Right}/>
    </div>;
  }

  return <div className={`skill-node ${selected ? 'selected' : ''} ${data.enabled === false ? 'disabled' : ''}`} data-tone={visual.tone}>
    <div className="node-heading">
      <span className="node-glyph" aria-hidden="true"><Icon size={16}/></span>
      <div className="node-heading-text"><small>{toneNames[visual.tone]}</small><strong>{title}</strong></div>
      {data.kind !== 'buff-events' && !data.definitionRoot &&
        <button className="nodrag icon-button" aria-label={`删除${title}节点`} onClick={() => data.remove(data.id)}><X size={13} aria-hidden="true"/></button>}
    </div>
    <div className="node-ports">
      {schema.inputs.filter(port => !data.definitionRoot).map(port => <div className="port-row" key={port}>
        <Handle type="target" id={port} position={Position.Left}/><span>{portName(port)}</span>
      </div>)}
      {ports.main.map(output)}
      {ports.extra.length > 0 && <details className="node-extra-events nodrag nowheel" onToggle={updatePorts}>
        <summary>更多事件 · {ports.extra.length}</summary>{ports.extra.map(output)}
      </details>}
    </div>
    <NodeParameters data={data} onToggle={updatePorts}/>
  </div>;
}
