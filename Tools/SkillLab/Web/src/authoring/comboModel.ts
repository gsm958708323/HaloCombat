import {deliveryLabel} from '../model';
import type {AuthoredTimeline, ComboCondition, ComboWindow, Project, RuntimeCombo, Skill} from '../model';

export const IDLE_NODE_ID = '$idle';
export const COMBO_CONFIRM_TAG = 1017;

export const COMMON_TAGS: {id: number; label: string; short: string}[] = [
  {id: 1017, label: '本次命中确认', short: '命中确认'},
  {id: 1002, label: '地面状态', short: '地面'},
  {id: 1003, label: '空中状态', short: '空中'},
  {id: 1004, label: '霸体状态', short: '霸体'},
  {id: 1009, label: '无敌状态', short: '无敌'},
  {id: 1006, label: '施法中', short: '施法中'},
];

export const CANCEL_CHANNELS: {id: string; label: string; color: string}[] = [
  {id: 'dodge', label: '闪避取消 (dodge)', color: '#6cc8a2'},
  {id: 'delay', label: '停顿派生 (delay)', color: '#b48ee0'},
  {id: 'special', label: '特殊取消 (special)', color: '#6eb8d8'},
];

const NAMED_INPUT_COLORS: Record<string, string> = {
  ArenaPrimary: '#e0b96b',
  Attack: '#e0b96b',
  ConfirmAttack: '#d9a45b',
  ArenaArea: '#6eb8d8',
  ArenaAura: '#b48ee0',
  ArenaDodge: '#6cc8a2',
  Dodge: '#6cc8a2',
  ArenaHoming: '#e08c7a',
  ArenaTracked: '#78b8c9',
  ArenaSummon: '#a89be8',
  ArenaConstruct: '#d99a6c',
  ArenaOrb: '#7fc9b8',
};

const FALLBACK_COLORS = ['#e0b96b', '#6eb8d8', '#b48ee0', '#6cc8a2', '#e08c7a', '#8cb4e0', '#d4a373', '#96c98c'];

export function inputColor(input: string): string {
  if (NAMED_INPUT_COLORS[input]) return NAMED_INPUT_COLORS[input];
  let hash = 0;
  for (let i = 0; i < input.length; i++) hash = (hash * 31 + input.charCodeAt(i)) | 0;
  return FALLBACK_COLORS[Math.abs(hash) % FALLBACK_COLORS.length];
}

export function channelColor(channel?: string): string {
  if (!channel) return '#d9bc78';
  const preset = CANCEL_CHANNELS.find(item => item.id === channel);
  if (preset) return preset.color;
  return inputColor('channel:' + channel);
}

export function inputShortLabel(project: Project, input: string): string {
  const actions = project.runtime?.arena?.bindings.filter(b => b.input === input).map(b => b.action) ?? [];
  return actions.length ? actions[0] : input;
}

export function inputFullLabel(project: Project, input: string): string {
  const actions = project.runtime?.arena?.bindings.filter(b => b.input === input).map(b => b.action) ?? [];
  return actions.length ? `${actions.join(' / ')} · ${input}` : input;
}

export function knownInputTokens(project: Project): string[] {
  const ordered: string[] = [];
  const add = (value: string) => { if (value && !ordered.includes(value)) ordered.push(value); };
  for (const binding of project.runtime?.arena?.bindings ?? []) add(binding.input);
  for (const rule of project.runtime?.combos ?? []) add(rule.input);
  if (!ordered.length) add('Attack');
  return ordered;
}

export function tagLabel(tag: number): string {
  const found = COMMON_TAGS.find(item => item.id === tag);
  return found ? found.short : `Tag ${tag}`;
}

export function conditionBadge(cond: ComboCondition, project: Project): {icon: string; text: string; tone: string} {
  if (cond.kind === 'tag') {
    const tag = cond.tag ?? COMBO_CONFIRM_TAG;
    if (tag === COMBO_CONFIRM_TAG) {
      return cond.negate
        ? {icon: '○', text: '挥空', tone: 'muted'}
        : {icon: '◎', text: '命中', tone: 'gold'};
    }
    return {icon: cond.negate ? '⊘' : '◈', text: (cond.negate ? '非' : '') + tagLabel(tag), tone: cond.negate ? 'warn' : 'info'};
  }
  if (cond.kind === 'buff') {
    const buff = project.buffs?.find(item => item.id === cond.buff);
    const name = buff?.name ?? cond.buff ?? 'Buff';
    const op = cond.op === 'eq' ? '=' : cond.op === 'lt' ? '<' : '≥';
    const val = cond.value ?? 1;
    if (!cond.negate && cond.op === 'eq' && val === 0) return {icon: '⊘', text: `无${name}`, tone: 'warn'};
    return {icon: '✦', text: `${cond.negate ? '非 ' : ''}${name}${op}${val}`, tone: 'violet'};
  }
  const resName = cond.resource === 'ammo' ? '弹药' : '能量';
  const op = cond.op === 'lte' ? '≤' : cond.op === 'lt' ? '<' : cond.op === 'eq' ? '=' : '≥';
  return {icon: '⚡', text: `${cond.negate ? '非 ' : ''}${resName}${op}${cond.value ?? 0}`, tone: 'cyan'};
}

export function windowBadge(window?: ComboWindow): {text: string; color: string} | null {
  if (!window || window.mode === 'default') return null;
  if (window.mode === 'anytime') return {text: '任意时刻', color: '#e08c7a'};
  if (window.mode === 'channel') return {text: window.channel || '命名轨', color: channelColor(window.channel)};
  return {text: `${(window.start ?? 0).toFixed(2)}–${(window.end ?? 0).toFixed(2)}s`, color: '#d9bc78'};
}

export interface CancelWindowSegment {
  start: number;
  end: number;
  channel?: string;
}

export interface SkillComboMeta {
  accent: string;
  typeLabel: string;
  duration: number;
  timelineKind: 'inline' | 'shared' | 'auto' | 'operation';
  timeline?: AuthoredTimeline;
  sharedTimelineName?: string;
  sharedUserCount: number;
  cancelSegments: CancelWindowSegment[];
}

export function skillMeta(project: Project, skill: Skill): SkillComboMeta {
  const cast = skill.nodes.find(n => n.kind === 'cast' && n.enabled !== false) ?? skill.nodes.find(n => n.kind === 'cast');
  const shared = skill.timelineRef ? project.timelines?.find(t => t.id === skill.timelineRef) : undefined;
  const actionNodes = shared ? shared.nodes : skill.nodes;
  const deliveryNode = actionNodes.find(n => n.enabled !== false && (n.kind === 'projectile' || n.kind === 'projectile-ref' || n.kind === 'area-ref' || n.kind === 'delivery' || n.kind === 'construct' || n.kind === 'teleport-projectile'));
  const typeLabel = deliveryNode ? deliveryLabel(deliveryNode) : (cast?.parameters.execution === 'operation' ? '即时操作' : '效果');
  const accent = !deliveryNode ? '#7b8d95'
    : deliveryNode.kind === 'projectile' || deliveryNode.kind === 'projectile-ref' ? '#d4a359'
    : deliveryNode.kind === 'area-ref' || deliveryNode.parameters.mode === 'area' ? '#70a87b'
    : deliveryNode.parameters.mode === 'melee' ? '#d46e6e'
    : deliveryNode.parameters.mode === 'displacement' || deliveryNode.kind === 'teleport-projectile' ? '#5cb8b2'
    : deliveryNode.parameters.mode === 'summon' ? '#9e8ad4'
    : deliveryNode.parameters.mode === 'ray' ? '#6fa8dc'
    : '#b58d67';

  if (shared) {
    const sharedUserCount = project.skills.filter(s => s.timelineRef === shared.id).length;
    return {
      accent, typeLabel, duration: shared.timeline.duration, timelineKind: 'shared', timeline: shared.timeline,
      sharedTimelineName: shared.name, sharedUserCount,
      cancelSegments: shared.timeline.clips.filter(c => c.kind === 'cancel').map(c => ({start: c.start, end: c.end, channel: c.channel})),
    };
  }
  if (skill.timeline) {
    return {
      accent, typeLabel, duration: skill.timeline.duration, timelineKind: 'inline', timeline: skill.timeline,
      sharedUserCount: 1,
      cancelSegments: skill.timeline.clips.filter(c => c.kind === 'cancel').map(c => ({start: c.start, end: c.end, channel: c.channel})),
    };
  }
  if (cast?.parameters.execution === 'operation') {
    return {accent, typeLabel, duration: 0, timelineKind: 'operation', sharedUserCount: 1, cancelSegments: []};
  }
  const duration = Number(cast?.parameters.duration ?? 0.3);
  const cancelStart = Number(cast?.parameters.cancelStart ?? 0);
  const cancelEnd = Number(cast?.parameters.cancelEnd ?? 0);
  return {
    accent, typeLabel, duration: Number.isFinite(duration) && duration > 0 ? duration : 0.3, timelineKind: 'auto', sharedUserCount: 1,
    cancelSegments: cancelEnd > cancelStart ? [{start: cancelStart, end: cancelEnd}] : [],
  };
}

export interface ComboChainGroup {
  id: string;
  label: string;
  skillIds: Set<string>;
  ruleIds: Set<string>;
  standalone?: boolean;
}

export function detectComboChains(project: Project, rules: readonly RuntimeCombo[], cancelRuleIds: ReadonlySet<string>): ComboChainGroup[] {
  const transitions = rules.filter(r => r.preSkills.length > 0 && !cancelRuleIds.has(r.id));
  const entries = rules.filter(r => r.preSkills.length === 0);
  const adj = new Map<string, Set<string>>();
  const touch = (id: string) => { if (!adj.has(id)) adj.set(id, new Set()); return adj.get(id)!; };
  for (const r of transitions) {
    touch(r.toSkill);
    for (const pre of r.preSkills) {
      touch(pre).add(r.toSkill);
      touch(r.toSkill).add(pre);
    }
  }
  const visited = new Set<string>();
  const chains: ComboChainGroup[] = [];
  const names = new Map(project.skills.map(s => [s.id, s.name]));
  const entryTargets = new Set(entries.map(e => e.toSkill));

  for (const skillId of adj.keys()) {
    if (visited.has(skillId)) continue;
    const component = new Set<string>();
    const queue = [skillId];
    visited.add(skillId);
    while (queue.length) {
      const cur = queue.shift()!;
      component.add(cur);
      for (const next of adj.get(cur) ?? []) {
        if (!visited.has(next)) { visited.add(next); queue.push(next); }
      }
    }
    const incomingInComp = new Set<string>();
    const ruleIds = new Set<string>();
    for (const r of transitions) {
      if (component.has(r.toSkill) && r.preSkills.some(p => component.has(p))) {
        incomingInComp.add(r.toSkill);
        ruleIds.add(r.id);
      }
    }
    for (const e of entries) if (component.has(e.toSkill)) ruleIds.add(e.id);
    const rootSkill = [...component].find(id => entryTargets.has(id) && !incomingInComp.has(id))
      ?? [...component].find(id => !incomingInComp.has(id))
      ?? [...component].find(id => entryTargets.has(id))
      ?? [...component][0];
    chains.push({
      id: 'chain:' + rootSkill,
      label: `${names.get(rootSkill) ?? rootSkill}链`,
      skillIds: component,
      ruleIds,
    });
  }

  const chainedSkills = new Set(chains.flatMap(c => [...c.skillIds]));
  const standaloneEntries = entries.filter(e => !chainedSkills.has(e.toSkill));
  if (standaloneEntries.length) {
    chains.push({
      id: 'standalone',
      label: '单发起手',
      skillIds: new Set(standaloneEntries.map(e => e.toSkill)),
      ruleIds: new Set(standaloneEntries.map(e => e.id)),
      standalone: true,
    });
  }
  return chains;
}

export function preSkillsOverlap(left: readonly string[], right: readonly string[]): boolean {
  if (!left.length || !right.length) return left.length === right.length;
  return left.some(id => right.includes(id));
}

export function conflictingPeers(rules: readonly RuntimeCombo[], target: Pick<RuntimeCombo, 'id' | 'input' | 'preSkills'>): RuntimeCombo[] {
  return rules
    .filter(row => row.input === target.input && preSkillsOverlap(row.preSkills, target.preSkills))
    .sort((a, b) => b.priority - a.priority || a.id.localeCompare(b.id));
}

export function priorityConflict(rules: readonly RuntimeCombo[], target: RuntimeCombo): RuntimeCombo | undefined {
  return rules.find(row => row.id !== target.id && row.input === target.input && row.priority === target.priority && preSkillsOverlap(row.preSkills, target.preSkills));
}

export function nextSafePriority(rules: readonly RuntimeCombo[], preSkills: readonly string[], input: string, ignoreId?: string): number {
  const peers = rules.filter(row => row.id !== ignoreId && row.input === input && preSkillsOverlap(row.preSkills, preSkills));
  const used = new Set(peers.map(row => row.priority));
  if (!used.has(0) && !peers.length) return preSkills.length ? 10 : 0;
  if (!used.has(10) && preSkills.length) return 10;
  let candidate = Math.max(0, ...peers.map(row => row.priority)) + 10;
  while (used.has(candidate)) candidate++;
  return candidate;
}

export function knownCancelChannels(project: Project): string[] {
  const set = new Set<string>(CANCEL_CHANNELS.map(c => c.id));
  for (const skill of project.skills) {
    for (const clip of skill.timeline?.clips ?? []) if (clip.kind === 'cancel' && clip.channel) set.add(clip.channel);
  }
  for (const tl of project.timelines ?? []) {
    for (const clip of tl.timeline.clips) if (clip.kind === 'cancel' && clip.channel) set.add(clip.channel);
  }
  for (const rule of project.runtime?.combos ?? []) {
    if (rule.window?.mode === 'channel' && rule.window.channel) set.add(rule.window.channel);
  }
  return [...set];
}
