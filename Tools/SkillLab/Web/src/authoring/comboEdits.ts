import type {Project, RuntimeCombo} from '../model';
import {maintainComboViews} from './comboViews';

export function withComboRuntime(project: Project): Project {
  if (project.runtime) return project;
  const cues = new Map<number, {id: number; key: string; kind: string; lifetime: number}>();
  const graphs = [...project.skills, ...(project.buffs ?? []), ...(project.timelines ?? []), ...(project.projectiles ?? []), ...(project.areas ?? [])];
  for (const graph of graphs) for (const node of graph.nodes) {
    const id = Number(node.kind === 'cue' ? node.parameters.id ?? 1 : node.parameters.cueId);
    if (Number.isInteger(id) && id > 0) cues.set(id, {id, key: 'cue-' + id, kind: 'cube', lifetime: .45});
  }
  return {...project, runtime: {motor: {gravity: -20, jumpSpeed: 6, airSteer: .35, groundY: 0, stickDeadzone: .25}, actors: [], combos: [], cues: [...cues.values()]}};
}

const ruleFields = ['id', 'purpose', 'preSkills', 'input', 'requiredTags', 'priority', 'toSkill'] as const;

function parseRule(raw: string): RuntimeCombo {
  const value: unknown = JSON.parse(raw);
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('请输入一个 ComboEntry JSON 对象');
  // JSON.NET recognizes property names case-insensitively and supplies defaults for omitted fields.
  // Mirror those defaults without allowing aliases to overwrite another value in the browser.
  const fields = new Map<string, unknown>();
  for (const [key, fieldValue] of Object.entries(value)) {
    const canonical = ruleFields.find(name => name.toLowerCase() === key.toLowerCase());
    if (!canonical) continue; // The untouched request body lets the server reject unknown keys.
    if (fields.has(canonical)) throw new Error(`规则字段重复：${canonical}`);
    fields.set(canonical, fieldValue);
  }
  const read = (key: typeof ruleFields[number], fallback?: unknown) => fields.has(key) ? fields.get(key) : fallback;
  const id = read('id'), input = read('input', 'Attack'), toSkill = read('toSkill');
  const preSkills = read('preSkills', []), requiredTags = read('requiredTags', []), priority = read('priority', 0), purpose = read('purpose');
  if (typeof id !== 'string' || typeof input !== 'string' || typeof toSkill !== 'string') throw new Error('id、input、toSkill 必须是字符串');
  if (!Array.isArray(preSkills) || preSkills.some(skill => typeof skill !== 'string')) throw new Error('preSkills 必须是技能 ID 字符串数组');
  if (!Array.isArray(requiredTags) || requiredTags.some(tag => !isInt32(tag) || tag <= 0)) throw new Error('requiredTags 必须是正 32 位整数数组');
  if (!isInt32(priority)) throw new Error('priority 必须是 32 位整数');
  if (purpose != null && purpose !== 'entry' && purpose !== 'transition' && purpose !== 'cancel') throw new Error('purpose 必须为 entry、transition 或 cancel，也可省略');
  return {id, ...(purpose == null ? {} : {purpose}), preSkills, input, requiredTags, priority, toSkill};
}

function isInt32(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= -2147483648 && value <= 2147483647;
}

export function prepareComboEdit(project: Project, originalId: string | undefined, raw: string | null) {
  const rule = raw === null ? null : parseRule(raw);
  const runtime = withComboRuntime(project).runtime!;
  const rows: RuntimeCombo[] = runtime.combos.slice();
  const index = originalId === undefined ? rows.length : rows.findIndex(row => row.id === originalId);
  if (index < 0) throw new Error('当前规则已不存在');
  if (rule && rows.some((row, i) => i !== index && row.id === rule.id)) throw new Error('规则 ID 已被其他接续使用');
  if (rule && originalId === undefined && !rule.preSkills.length) throw new Error('新增接续需要至少一个前置技能');
  if (rule) rows[index] = rule;
  else rows.splice(index, 1);
  const rename = rule && originalId !== undefined && originalId !== rule.id ? {from: originalId, to: rule.id} : undefined;
  const next = maintainComboViews({...project, runtime: {...runtime, combos: rows}}, rename);
  // Keep the edited object verbatim for the shared compiler's strict JSON checks.
  // Only serialize the surrounding document and other rules.
  const list = rows.map((row, i) => rule && i === index ? raw : JSON.stringify(row)).join(',');
  const json = JSON.stringify({...next, runtime: undefined}).slice(0, -1) + ',"runtime":' +
    JSON.stringify({...runtime, combos: undefined}).slice(0, -1) + ',"combos":[' + list + ']}}';
  return {project: next, rule, json};
}
