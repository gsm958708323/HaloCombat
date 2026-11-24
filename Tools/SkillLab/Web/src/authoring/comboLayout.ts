import type {ComboPosition, RuntimeCombo} from '../model';
import {IDLE_NODE_ID} from './comboModel';

export type ComboEdgeRoute = 'entry' | 'forward' | 'return' | 'loop';

export interface ComboDagEdge {
  id: string;
  source: string;
  target: string;
  rule: RuntimeCombo;
  lane: number;
  laneCount: number;
  route: ComboEdgeRoute;
  isCancelOverlay?: boolean;
}

export interface ComboDagLayout {
  skillIds: string[];
  positions: Map<string, {x: number; y: number}>;
  edges: ComboDagEdge[];
  edgesById: Map<string, ComboDagEdge>;
}

export const comboEdgeId = (ruleId: string, source: string) => JSON.stringify([ruleId, source]);

const COL_GAP = 310;
const ROW_GAP = 128;

export function computeAutoPositions(
  rules: readonly RuntimeCombo[],
  visibleSkillIds: readonly string[],
  cancelRuleIds: ReadonlySet<string>,
): Map<string, {x: number; y: number}> {
  const skillSet = new Set(visibleSkillIds);
  const transitions = rules.filter(r => r.preSkills.length > 0 && !cancelRuleIds.has(r.id) && skillSet.has(r.toSkill));
  const entries = rules.filter(r => r.preSkills.length === 0 && skillSet.has(r.toSkill));

  const outgoing = new Map<string, RuntimeCombo[]>();
  const incomingCount = new Map<string, number>();
  for (const id of visibleSkillIds) { outgoing.set(id, []); incomingCount.set(id, 0); }
  for (const rule of transitions) {
    for (const pre of new Set(rule.preSkills)) {
      if (!skillSet.has(pre)) continue;
      outgoing.get(pre)!.push(rule);
      if (pre !== rule.toSkill) incomingCount.set(rule.toSkill, (incomingCount.get(rule.toSkill) ?? 0) + 1);
    }
  }

  const entryTargets = new Set(entries.map(e => e.toSkill));
  const hasTransition = (id: string) => (outgoing.get(id)?.length ?? 0) > 0 || (incomingCount.get(id) ?? 0) > 0;

  // Order roots: combo chain starters first (sorted by entry first, then most outgoing), then standalone entries, then pinned unlinked skills.
  const roots: string[] = [];
  const added = new Set<string>();
  const pushRoot = (id: string) => { if (!added.has(id) && skillSet.has(id)) { added.add(id); roots.push(id); } };

  const chainStarters = visibleSkillIds.filter(id => hasTransition(id) && (incomingCount.get(id) ?? 0) === 0);
  chainStarters.sort((a, b) => Number(entryTargets.has(b)) - Number(entryTargets.has(a)) || (outgoing.get(b)?.length ?? 0) - (outgoing.get(a)?.length ?? 0));
  for (const id of chainStarters) pushRoot(id);

  // Cycle roots (connected skills where every node has an incoming edge)
  for (const id of visibleSkillIds) if (hasTransition(id) && entryTargets.has(id)) pushRoot(id);
  for (const id of visibleSkillIds) if (hasTransition(id)) pushRoot(id);

  const placed = new Set<string>();
  const usedCells = new Map<number, Set<number>>();
  const positions = new Map<string, {x: number; y: number}>();
  const occupy = (id: string, col: number, row: number) => {
    placed.add(id);
    let rows = usedCells.get(col);
    if (!rows) usedCells.set(col, rows = new Set());
    rows.add(row);
    positions.set(id, {x: col * COL_GAP, y: row * ROW_GAP});
  };

  let cursorRow = 0;

  function placeChain(startId: string, col: number, nearRow: number, incomingInput?: string): number {
    // Follow primary linear chain first on a straight horizontal row.
    const chain: {id: string; col: number; input?: string}[] = [];
    let cur: string | undefined = startId;
    let curCol = col;
    let curInput = incomingInput;
    const inChain = new Set<string>();
    while (cur && !placed.has(cur) && !inChain.has(cur)) {
      inChain.add(cur);
      chain.push({id: cur, col: curCol, input: curInput});
      const outs: RuntimeCombo[] = (outgoing.get(cur) ?? []).filter(r => r.toSkill !== cur && !placed.has(r.toSkill) && !inChain.has(r.toSkill));
      if (!outs.length) break;
      const sameInput: RuntimeCombo | undefined = curInput ? outs.find(r => r.input === curInput) : undefined;
      const picked: RuntimeCombo = sameInput ?? outs[0];
      cur = picked.toSkill;
      curCol += 1;
      curInput = picked.input;
    }

    let row = nearRow;
    while (chain.some(step => usedCells.get(step.col)?.has(row))) row++;
    for (const step of chain) occupy(step.id, step.col, row);
    let maxRow = row;

    for (const step of chain) {
      const outs = (outgoing.get(step.id) ?? []).filter(r => !placed.has(r.toSkill));
      const seenTargets = new Set<string>();
      for (const rule of outs) {
        if (seenTargets.has(rule.toSkill) || placed.has(rule.toSkill)) continue;
        seenTargets.add(rule.toSkill);
        maxRow = Math.max(maxRow, placeChain(rule.toSkill, step.col + 1, row + 1, rule.input));
      }
    }
    return maxRow;
  }

  for (const root of roots) {
    if (placed.has(root)) continue;
    const entryInput = entries.find(e => e.toSkill === root)?.input;
    const last = placeChain(root, 1, cursorRow, entryInput);
    cursorRow = last + 1.4;
  }

  // Place standalone entry skills & manually pinned skills cleanly in a compact 2-column grid below chains.
  const rest = visibleSkillIds.filter(id => !placed.has(id));
  if (rest.length) {
    const startRow = cursorRow > 0 ? Math.ceil(cursorRow) + 0.3 : 0;
    const cols = rest.length > 4 ? 2 : 1;
    rest.forEach((id, i) => {
      const col = 1 + (i % cols);
      const row = startRow + Math.floor(i / cols);
      occupy(id, col, row);
    });
  }

  const allYs = [...positions.values()].map(p => p.y);
  const idleY = allYs.length ? Math.round((Math.min(...allYs) + Math.min(Math.max(...allYs), 240)) / 2) : 0;
  positions.set(IDLE_NODE_ID, {x: 0, y: idleY});
  return positions;
}

export function buildComboDagLayout(
  rules: readonly RuntimeCombo[],
  visibleSkillIds: readonly string[],
  cancelRuleIds: ReadonlySet<string>,
  savedPositions: readonly ComboPosition[] | undefined,
  showCancelOverlay: boolean,
): ComboDagLayout {
  const auto = computeAutoPositions(rules, visibleSkillIds, cancelRuleIds);
  const saved = new Map((savedPositions ?? []).map(p => [p.skillId, {x: p.x, y: p.y}]));
  const positions = new Map<string, {x: number; y: number}>();
  positions.set(IDLE_NODE_ID, saved.get(IDLE_NODE_ID) ?? auto.get(IDLE_NODE_ID) ?? {x: 0, y: 0});
  for (const id of visibleSkillIds) {
    positions.set(id, saved.get(id) ?? auto.get(id) ?? {x: COL_GAP, y: 0});
  }

  const skillSet = new Set(visibleSkillIds);
  const rawLinks: {id: string; source: string; target: string; rule: RuntimeCombo; isCancelOverlay: boolean}[] = [];

  for (const rule of rules) {
    if (!skillSet.has(rule.toSkill)) continue;
    const isCancel = cancelRuleIds.has(rule.id);
    if (isCancel && !showCancelOverlay) continue;
    if (!rule.preSkills.length) {
      rawLinks.push({id: comboEdgeId(rule.id, IDLE_NODE_ID), source: IDLE_NODE_ID, target: rule.toSkill, rule, isCancelOverlay: false});
    } else {
      for (const source of new Set(rule.preSkills)) {
        if (!skillSet.has(source)) continue;
        rawLinks.push({id: comboEdgeId(rule.id, source), source, target: rule.toSkill, rule, isCancelOverlay: isCancel});
      }
    }
  }

  // Group parallel edges between the same (source, target) pair for lane spreading.
  const pairGroups = new Map<string, typeof rawLinks>();
  for (const link of rawLinks) {
    const key = `${link.source}\0${link.target}`;
    let list = pairGroups.get(key);
    if (!list) pairGroups.set(key, list = []);
    list.push(link);
  }

  const edges: ComboDagEdge[] = [];
  const edgesById = new Map<string, ComboDagEdge>();
  for (const group of pairGroups.values()) {
    group.sort((a, b) => b.rule.priority - a.rule.priority || a.id.localeCompare(b.id));
    group.forEach((link, lane) => {
      const fromPos = positions.get(link.source)!;
      const toPos = positions.get(link.target)!;
      const route: ComboEdgeRoute = link.source === IDLE_NODE_ID ? 'entry'
        : link.source === link.target ? 'loop'
        : fromPos.x >= toPos.x - 40 ? 'return'
        : 'forward';
      const edge: ComboDagEdge = {...link, lane, laneCount: group.length, route};
      edges.push(edge);
      edgesById.set(edge.id, edge);
    });
  }

  return {skillIds: [...visibleSkillIds], positions, edges, edgesById};
}
