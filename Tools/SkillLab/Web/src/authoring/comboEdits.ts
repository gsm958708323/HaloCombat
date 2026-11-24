import type {ComboCondition, ComboWindow, Project, RuntimeCombo} from '../model';
import {maintainComboViews} from './comboViews';
import {nextSafePriority, preSkillsOverlap} from './comboModel';

export function withComboRuntime(project: Project): Project {
  if (project.runtime) return project;
  const cues = new Map<number, {id: number; key: string; kind: string; lifetime: number}>();
  const graphs = [...project.skills, ...(project.buffs ?? []), ...(project.timelines ?? []), ...(project.projectiles ?? []), ...(project.areas ?? [])];
  for (const graph of graphs) for (const node of graph.nodes) {
    const id = Number(node.kind === 'cue' ? node.parameters.id ?? 1 : node.parameters.cueId);
    if (Number.isInteger(id) && id > 0) cues.set(id, {id, key: 'cue-' + id, kind: 'cube', lifetime: .45});
  }
  return {
    ...project,
    runtime: {
      motor: {gravity: -20, jumpSpeed: 6, airSteer: .35, groundY: 0, stickDeadzone: .25},
      actors: [],
      combos: [],
      cues: [...cues.values()],
    },
  };
}

function makeRuleId(existing: readonly RuntimeCombo[], prefix: string): string {
  const used = new Set(existing.map(row => row.id));
  const clean = prefix.replace(/[^a-zA-Z0-9_-]+/g, '-').replace(/^-+|-+$/g, '') || 'combo';
  if (!used.has(clean)) return clean;
  for (let i = 2; ; i++) {
    const candidate = `${clean}-${i}`;
    if (!used.has(candidate)) return candidate;
  }
}

export function addEntryRule(project: Project, toSkill: string, input?: string): {project: Project; rule: RuntimeCombo} {
  const base = withComboRuntime(project);
  const runtime = base.runtime!;
  const chosenInput = input || runtime.arena?.bindings[0]?.input || runtime.combos[0]?.input || 'Attack';
  const rule: RuntimeCombo = {
    id: makeRuleId(runtime.combos, `entry-${toSkill}`),
    preSkills: [],
    input: chosenInput,
    conditions: [],
    priority: nextSafePriority(runtime.combos, [], chosenInput),
    toSkill,
  };
  return {
    project: {...base, runtime: {...runtime, combos: [...runtime.combos, rule]}},
    rule,
  };
}

export function addTransitionRule(project: Project, fromSkill: string, toSkill: string, input?: string): {project: Project; rule: RuntimeCombo} {
  const base = withComboRuntime(project);
  const runtime = base.runtime!;
  const incoming = runtime.combos.find(r => r.toSkill === fromSkill);
  const chosenInput = input || incoming?.input || runtime.arena?.bindings[0]?.input || runtime.combos[0]?.input || 'Attack';
  const preSkills = [fromSkill];
  const rule: RuntimeCombo = {
    id: makeRuleId(runtime.combos, `${fromSkill}-to-${toSkill}`),
    preSkills,
    input: chosenInput,
    conditions: [],
    priority: nextSafePriority(runtime.combos, preSkills, chosenInput),
    toSkill,
  };
  return {
    project: {...base, runtime: {...runtime, combos: [...runtime.combos, rule]}},
    rule,
  };
}

export function addCancelRule(project: Project, toSkill: string, input?: string, preSkills?: string[]): {project: Project; rule: RuntimeCombo} {
  const base = withComboRuntime(project);
  const runtime = base.runtime!;
  const chosenInput = input || runtime.arena?.bindings.find(b => b.input.toLowerCase().includes('dodge'))?.input || 'ArenaDodge';
  const defaultPre = preSkills?.length ? preSkills : project.skills.filter(s => s.id !== toSkill).slice(0, 1).map(s => s.id);
  const finalPre = defaultPre.length ? defaultPre : [toSkill];
  const rule: RuntimeCombo = {
    id: makeRuleId(runtime.combos, `cancel-${toSkill}`),
    preSkills: finalPre,
    input: chosenInput,
    conditions: [],
    window: {mode: 'channel', channel: 'dodge'},
    priority: nextSafePriority(runtime.combos, finalPre, chosenInput),
    toSkill,
  };
  const cancelIds = [...new Set([...(base.editor?.comboCancelRuleIds ?? []), rule.id])];
  return {
    project: {
      ...base,
      runtime: {...runtime, combos: [...runtime.combos, rule]},
      editor: {...base.editor, comboCancelRuleIds: cancelIds},
    },
    rule,
  };
}

export function updateComboRule(project: Project, ruleId: string, patch: Partial<RuntimeCombo>, autoResolvePriority = true): Project {
  const base = withComboRuntime(project);
  const runtime = base.runtime!;
  const index = runtime.combos.findIndex(row => row.id === ruleId);
  if (index < 0) return project;
  const current = runtime.combos[index];
  const next: RuntimeCombo = {...current, ...patch};
  if (next.window?.mode === 'default') delete next.window;
  if (autoResolvePriority && patch.priority === undefined) {
    const collides = runtime.combos.some((row, i) => i !== index && row.input === next.input && row.priority === next.priority && preSkillsOverlap(row.preSkills, next.preSkills));
    if (collides) next.priority = nextSafePriority(runtime.combos, next.preSkills, next.input, ruleId);
  }
  const combos = runtime.combos.slice();
  combos[index] = next;
  const rename = next.id !== ruleId ? {from: ruleId, to: next.id} : undefined;
  return maintainComboViews({...base, runtime: {...runtime, combos}}, rename);
}

export function deleteComboRule(project: Project, ruleId: string): Project {
  const runtime = project.runtime;
  if (!runtime) return project;
  const combos = runtime.combos.filter(row => row.id !== ruleId);
  return maintainComboViews({...project, runtime: {...runtime, combos}});
}

export function toggleCancelBarMembership(project: Project, ruleId: string): Project {
  const rule = project.runtime?.combos.find(r => r.id === ruleId);
  if (!rule || !rule.preSkills.length) return project;
  const current = new Set(project.editor?.comboCancelRuleIds ?? []);
  if (current.has(ruleId)) current.delete(ruleId);
  else current.add(ruleId);
  const next = [...current];
  return {
    ...project,
    editor: {...project.editor, comboCancelRuleIds: next.length ? next : undefined},
  };
}

export function toggleCancelRulePreSkill(project: Project, ruleId: string, skillId: string): Project {
  const rule = project.runtime?.combos.find(r => r.id === ruleId);
  if (!rule) return project;
  const has = rule.preSkills.includes(skillId);
  if (has && rule.preSkills.length <= 1) return project; // Keep at least one predecessor so it remains a cancel rule.
  const preSkills = has ? rule.preSkills.filter(id => id !== skillId) : [...rule.preSkills, skillId];
  return updateComboRule(project, ruleId, {preSkills}, true);
}

export function reorderPeerPriorities(project: Project, orderedRuleIds: readonly string[]): Project {
  const runtime = project.runtime;
  if (!runtime || orderedRuleIds.length < 2) return project;
  const count = orderedRuleIds.length;
  const rankMap = new Map<string, number>();
  orderedRuleIds.forEach((id, i) => rankMap.set(id, (count - i) * 10));
  const combos = runtime.combos.map(row => rankMap.has(row.id) ? {...row, priority: rankMap.get(row.id)!} : row);
  return {...project, runtime: {...runtime, combos}};
}

export function saveComboPositions(project: Project, updates: Record<string, {x: number; y: number}>): Project {
  const map = new Map((project.editor?.comboPositions ?? []).map(item => [item.skillId, item]));
  for (const [skillId, pos] of Object.entries(updates)) {
    map.set(skillId, {skillId, x: Math.round(pos.x), y: Math.round(pos.y)});
  }
  return {
    ...project,
    editor: {...project.editor, comboPositions: [...map.values()]},
  };
}

export function clearComboPositions(project: Project): Project {
  if (!project.editor?.comboPositions?.length) return project;
  return {...project, editor: {...project.editor, comboPositions: undefined}};
}

function isInt32(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= -2147483648 && value <= 2147483647;
}

export function parseRuleJson(raw: string): RuntimeCombo {
  const value: unknown = JSON.parse(raw);
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('请输入一个 ComboEntry JSON 对象');
  const obj = value as Record<string, unknown>;
  const id = obj.id, input = obj.input ?? 'Attack', toSkill = obj.toSkill;
  const preSkills = obj.preSkills ?? [], conditions = obj.conditions ?? [], priority = obj.priority ?? 0, window = obj.window;
  if (typeof id !== 'string' || !id.trim()) throw new Error('id 必须是非空字符串');
  if (typeof input !== 'string' || !input.trim()) throw new Error('input 必须是非空字符串');
  if (typeof toSkill !== 'string' || !toSkill.trim()) throw new Error('toSkill 必须是非空字符串');
  if (!Array.isArray(preSkills) || preSkills.some(s => typeof s !== 'string')) throw new Error('preSkills 必须是技能 ID 字符串数组');
  if (!Array.isArray(conditions)) throw new Error('conditions 必须是条件对象数组');
  if (!isInt32(priority)) throw new Error('priority 必须是 32 位整数');
  const parsedConditions: ComboCondition[] = conditions.map((item, idx) => {
    if (!item || typeof item !== 'object' || Array.isArray(item)) throw new Error(`conditions[${idx}] 必须是对象`);
    const c = item as Record<string, unknown>;
    if (c.kind !== 'tag' && c.kind !== 'buff' && c.kind !== 'resource') throw new Error(`conditions[${idx}].kind 无效`);
    return {
      kind: c.kind,
      ...(c.negate ? {negate: true} : {}),
      ...(c.tag !== undefined ? {tag: Number(c.tag)} : {}),
      ...(c.buff !== undefined ? {buff: String(c.buff)} : {}),
      ...(c.resource !== undefined ? {resource: c.resource as 'energy' | 'ammo'} : {}),
      ...(c.op !== undefined ? {op: c.op as 'gte' | 'eq' | 'lt' | 'lte'} : {}),
      ...(c.value !== undefined ? {value: Number(c.value)} : {}),
    };
  });
  let parsedWindow: ComboWindow | undefined;
  if (window != null) {
    if (typeof window !== 'object' || Array.isArray(window)) throw new Error('window 必须是对象');
    const w = window as Record<string, unknown>;
    const mode = (w.mode ?? 'default') as ComboWindow['mode'];
    if (!['default', 'channel', 'range', 'anytime'].includes(mode)) throw new Error('window.mode 无效');
    if (mode !== 'default') {
      parsedWindow = {
        mode,
        ...(w.channel !== undefined ? {channel: String(w.channel)} : {}),
        ...(w.start !== undefined ? {start: Number(w.start)} : {}),
        ...(w.end !== undefined ? {end: Number(w.end)} : {}),
      };
    }
  }
  return {
    id,
    preSkills: preSkills as string[],
    input,
    conditions: parsedConditions,
    ...(parsedWindow ? {window: parsedWindow} : {}),
    priority,
    toSkill,
  };
}
