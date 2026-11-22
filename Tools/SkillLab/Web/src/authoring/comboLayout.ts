import type {RuntimeCombo} from '../model';

interface Link {
  id: string;
  source: string;
  target: string;
  rule: RuntimeCombo;
}

export interface ComboLink extends Link {
  sourceId: string;
  targetId: string;
  lane: number;
  laneCount: number;
}

export interface ComboCard {
  id: string;
  skillId: string;
  reference: boolean;
  position: {x: number; y: number};
}

interface Branch {
  card: ComboCard;
  parent?: Branch;
  children: {branch: Branch; primary: boolean}[];
  depth: number;
}

export const comboLinkId = (ruleId: string, source: string) => JSON.stringify([ruleId, source]);

// Each skill owns its real outgoing edges once. Additional occurrences are terminal
// references local to one source/target pair and share the same authorable skill.
export function layoutCombo(rules: readonly RuntimeCombo[]) {
  const outgoing = new Map<string, Link[]>();
  const pairs = new Map<string, Map<string, Link[]>>();
  const links: Link[] = [];
  for (const rule of rules) {
    for (const source of new Set(rule.preSkills)) {
      const target = rule.toSkill;
      const link = {id: comboLinkId(rule.id, source), source, target, rule};
      if (!outgoing.has(source)) outgoing.set(source, []);
      if (!outgoing.has(target)) outgoing.set(target, []);
      outgoing.get(source)!.push(link);
      links.push(link);
      let targets = pairs.get(source);
      if (!targets) pairs.set(source, targets = new Map());
      let group = targets.get(target);
      if (!group) targets.set(target, group = []);
      group.push(link);
    }
  }

  const cards = new Map<string, ComboCard>();
  const mainCards = new Map<string, ComboCard>();
  const expanded: Branch[] = [];
  const linksById = new Map<string, ComboLink>();

  function makeBranch(skillId: string, parent?: Branch, reference = false): Branch {
    const id = reference ? 'reference:' + JSON.stringify([parent!.card.skillId, skillId]) : 'skill:' + skillId;
    const card: ComboCard = {id, skillId, reference, position: {x: 0, y: 0}};
    const branch: Branch = {card, parent, children: [], depth: parent ? parent.depth + 1 : 1};
    cards.set(id, card);
    if (!reference) {
      mainCards.set(skillId, card);
      expanded.push(branch);
    }
    return branch;
  }

  function attach(source: Branch, group: Link[], primary: boolean, input: string) {
    const {target} = group[0];
    // Keep self-loops and adjacent non-cancel returns on their existing card.
    // Longer returns and shared targets are represented where the branch starts.
    const local = target === source.card.skillId ? source :
      (source.parent?.card.skillId === target && group.every(link => link.rule.purpose !== 'cancel') ? source.parent : undefined);
    const destination = local ?? makeBranch(target, source, mainCards.has(target));
    if (!local) source.children.push({branch: destination, primary});
    group.forEach((link, lane) => linksById.set(link.id, {...link, sourceId: source.card.id,
      targetId: destination.card.id, lane, laneCount: group.length}));
    if (!local && !destination.card.reference) expandPrimary(destination, input);
  }

  function expandPrimary(source: Branch, incoming?: string) {
    const groups = [...(pairs.get(source.card.skillId)?.values() ?? [])].flatMap(group => {
      const primary = group.filter(link => link.rule.purpose !== 'cancel');
      if (!primary.length) return [];
      const matching = primary.find(link => link.rule.input === incoming);
      return [{group, input: (matching ?? primary[0]).rule.input, match: !!matching}];
    });
    groups.sort((left, right) => Number(right.match) - Number(left.match));
    for (const {group, input} of groups) attach(source, group, true, input);
  }

  const roots = sourceComponents(outgoing).map(skillId => makeBranch(skillId));
  for (const root of roots) expandPrimary(root);
  // Every normal route gets first choice of main cards across all independent roots.
  // The worklist grows when a cancel-only edge introduces an unvisited skill.
  for (const source of expanded) {
    for (const group of pairs.get(source.card.skillId)?.values() ?? []) {
      if (group.every(link => link.rule.purpose === 'cancel')) attach(source, group, false, group[0].rule.input);
    }
  }

  // A stack of parallel self-loops needs more room above a card.
  const selfLanes = Math.max(1, ...Array.from(pairs, ([skillId, targets]) => targets.get(skillId)?.length ?? 0));
  const rowHeight = 100 + (selfLanes - 1) * 16;
  let firstRow = 0;
  for (const root of roots) firstRow = placeTree(root, firstRow, rowHeight) + 2;

  return {cards, mainCards, roots: roots.map(root => root.card), linksById,
    edges: links.map(link => linksById.get(link.id)!), ruleCount: rules.filter(rule => rule.preSkills.length > 0).length};
}

// Keep a primary chain on one row. A side branch searches the occupied cells
// only in the columns it needs, so unrelated columns can share the same row.
function placeTree(root: Branch, firstRow: number, rowHeight: number) {
  const usedRows = new Map<number, Set<number>>();
  const cancelLeaves: Branch[] = [];
  let lastRow = firstRow;
  function occupy(branch: Branch, column: number, row: number) {
    let rows = usedRows.get(column);
    if (!rows) usedRows.set(column, rows = new Set());
    rows.add(row);
    branch.card.position = {x: column * 280, y: row * rowHeight};
    lastRow = Math.max(lastRow, row);
  }
  function placeChain(head: Branch, near: number) {
    const chain = [head];
    for (const branch of chain) {
      const first = branch.children[0];
      if (first?.primary) chain.push(first.branch);
    }
    let row = near;
    while (chain.some(branch => usedRows.get(branch.depth)?.has(row))) row++;
    for (const branch of chain) occupy(branch, branch.depth, row);
    for (const branch of chain) {
      const sideBranches = branch.children[0]?.primary ? branch.children.slice(1) : branch.children;
      for (const child of sideBranches) {
        if (!child.primary && child.branch.card.reference) cancelLeaves.push(child.branch);
        else placeChain(child.branch, row + 1);
      }
    }
  }
  placeChain(root, firstRow);
  // Give the first visible row first pick of the immediately adjacent spaces.
  cancelLeaves.sort((a, b) => a.parent!.card.position.y - b.parent!.card.position.y || a.depth - b.depth);
  for (const leaf of cancelLeaves) {
    // Keep the destination beside its source so the curve stays between columns.
    const parentRow = leaf.parent!.card.position.y / rowHeight;
    const rows = usedRows.get(leaf.depth);
    for (let distance = 0; ; distance++) {
      const row = [parentRow + distance, parentRow - distance].find(candidate => candidate >= firstRow && !rows?.has(candidate));
      if (row === undefined) continue;
      occupy(leaf, leaf.depth, row);
      break;
    }
  }
  return lastRow;
}

// Collapse strongly connected components to discover every real source component.
// The earliest configured member represents a source cycle, including a self-loop.
function sourceComponents(outgoing: Map<string, Link[]>) {
  const indices = new Map<string, number>();
  const low = new Map<string, number>();
  const stack: string[] = [];
  const onStack = new Set<string>();
  const component = new Map<string, number>();
  let count = 0;
  function visit(id: string) {
    const index = indices.size;
    indices.set(id, index);
    low.set(id, index);
    stack.push(id);
    onStack.add(id);
    for (const {target} of outgoing.get(id)!) {
      if (!indices.has(target)) {
        visit(target);
        low.set(id, Math.min(low.get(id)!, low.get(target)!));
      } else if (onStack.has(target)) {
        low.set(id, Math.min(low.get(id)!, indices.get(target)!));
      }
    }
    if (low.get(id) !== index) return;
    let member: string;
    do {
      member = stack.pop()!;
      onStack.delete(member);
      component.set(member, count);
    } while (member !== id);
    count++;
  }
  for (const id of outgoing.keys()) if (!indices.has(id)) visit(id);
  const downstream = new Set<number>();
  for (const links of outgoing.values()) {
    for (const {source, target} of links) {
      const destination = component.get(target)!;
      if (component.get(source) !== destination) downstream.add(destination);
    }
  }
  const roots: string[] = [];
  for (const id of outgoing.keys()) {
    const group = component.get(id)!;
    if (downstream.has(group)) continue;
    roots.push(id);
    downstream.add(group);
  }
  return roots;
}
