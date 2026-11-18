(() => {
  'use strict';

  const Model = window.LabModel;
  const CASTER = {x: -4, y: 0};
  const CROSS = [[0, 0], [0.8, -3.3], [1.4, -3.3], [2.16, 0], [2.58, 0], [3.4, -3.3]];
  const EPSILON = 1e-9;
  const clamp = (value, lo, hi) => Math.min(hi, Math.max(lo, value));
  const clock = time => Math.round(time * 1e6) / 1e6;
  const display = value => Number(value.toFixed(2));
  const lerp = (a, b, u) => a + (b - a) * u;
  const offset = (point, origin) => ({x: point.x - origin.x, y: point.y - origin.y});
  const lengthSquared = ({x, y}) => x * x + y * y;

  function numeric(value, fallback) {
    if (typeof value === 'string' && value.trim()) value = Number(value);
    return Number.isFinite(value) ? value : fallback;
  }

  function arenaPoint(point, fallback = CASTER) {
    return {x: clamp(numeric(point?.x, fallback.x), -6, 6), y: clamp(numeric(point?.y, fallback.y), -4, 4)};
  }

  function position(setup, target, time) {
    const anchor = arenaPoint(target, {x: 0, y: 0});
    if (setup.path !== 'cross' || target.id !== 'B') return anchor;
    const yAt = delta => clamp(anchor.y + delta, -4, 4);
    const t = Math.max(0, numeric(time, 0));
    for (let i = 1; i < CROSS.length; i++) {
      const [end, to] = CROSS[i];
      if (t <= end) {
        const [start, from] = CROSS[i - 1];
        return {x: anchor.x, y: lerp(yAt(from), yAt(to), (t - start) / (end - start))};
      }
    }
    return {x: anchor.x, y: yAt(CROSS.at(-1)[1])};
  }

  // Each leg is linear, including paths clipped at the arena edge. Subtracting a
  // moving center gives the same circle test for both regions and swept bullets.
  function motionLegs(setup, target, start, end, centerAt) {
    const cuts = [start];
    if (setup.path === 'cross' && target.id === 'B') {
      for (const [time] of CROSS) if (time > start && time < end) cuts.push(time);
    }
    cuts.push(end);
    const relative = time => offset(position(setup, target, time), centerAt(time));
    return cuts.slice(1).map((to, i) => {
      const from = cuts[i];
      return {from, to, a: relative(from), b: relative(to)};
    });
  }

  // Return the normalized interval in the circle, including tangential contact.
  function circleSpan(a, b, radius) {
    const delta = offset(b, a), distance = lengthSquared(delta);
    const radiusSquared = radius * radius;
    if (distance < EPSILON ** 2) return lengthSquared(a) <= radiusSquared + EPSILON ? [0, 1] : null;
    const closest = -(a.x * delta.x + a.y * delta.y) / distance;
    const nearest = {x: a.x + closest * delta.x, y: a.y + closest * delta.y};
    const reach = radiusSquared - lengthSquared(nearest);
    if (reach < -EPSILON) return null;
    const half = Math.sqrt(Math.max(reach, 0) / distance);
    const lo = Math.max(0, closest - half), hi = Math.min(1, closest + half);
    return lo <= hi + EPSILON ? [clamp(lo, 0, 1), clamp(hi, 0, 1)] : null;
  }

  function circleTimes(setup, target, start, end, centerAt, radius) {
    const intervals = [];
    for (const {from, to, a, b} of motionLegs(setup, target, start, end, centerAt)) {
      const span = circleSpan(a, b, radius);
      if (!span) continue;
      const lo = lerp(from, to, span[0]), hi = lerp(from, to, span[1]);
      const last = intervals.at(-1);
      if (last && lo <= last[1] + EPSILON) last[1] = Math.max(last[1], hi);
      else intervals.push([lo, hi]);
    }
    return intervals;
  }

  function firstSweep(setup, target, start, end, {range, angle}, direction) {
    const cos = Math.cos(direction), sin = Math.sin(direction);
    const local = ({x, y}) => ({x: cos * x + sin * y, y: cos * y - sin * x});
    const half = angle * Math.PI / 360;
    const forward = Math.cos(half), side = Math.sin(half);
    for (const leg of motionLegs(setup, target, start, end, () => CASTER)) {
      const a = local(leg.a), b = local(leg.b);
      const span = circleSpan(a, b, range);
      if (!span) continue;
      const [lo, hi] = span;
      const candidates = [lo, hi];
      // Membership can change only at the outer arc or either of the two rays.
      for (const sign of [-1, 1]) {
        const f = point => forward * point.y - sign * side * point.x;
        const before = f(a), after = f(b);
        if (Math.abs(after - before) <= EPSILON) continue;
        const crossing = before / (before - after);
        if (crossing >= lo - EPSILON && crossing <= hi + EPSILON) candidates.push(clamp(crossing, lo, hi));
      }
      for (const u of candidates.sort((left, right) => left - right)) {
        const x = lerp(a.x, b.x, u), y = lerp(a.y, b.y, u);
        const time = clock(lerp(leg.from, leg.to, u));
        if (time < clock(end) && x + EPSILON >= forward * Math.hypot(x, y)) return time;
      }
    }
    return null;
  }

  function effectiveParams(node) {
    const params = Model.makeNode(node.kind).params;
    for (const field of Model.catalog[node.kind].fields) {
      const value = node.params?.[field.key];
      if (field.type === 'number') {
        const bounded = clamp(numeric(value, params[field.key]), field.min, field.max);
        params[field.key] = field.key === 'count' ? Math.round(bounded) : bounded;
      } else if (field.options.some(option => option.value === value) ||
          (node.kind === 'burn' && typeof value === 'string' && value.trim())) {
        params[field.key] = value;
      }
    }
    return params;
  }

  function run(doc, setup = Model.setup(doc.templateId), shared = Model.sharedDefaults()) {
    const aim = arenaPoint(setup.aim, {x: 0, y: 0});
    const direction = Math.atan2(aim.y - CASTER.y, aim.x - CASTER.x);
    const actors = ['A', 'B'].map(id => setup.targets.find(target => target.id === id)).filter(Boolean).map(target => {
      const hp = Math.max(0, numeric(target.hp, 100)), shield = Math.max(0, numeric(target.shield, 0));
      return {target, hp, maxHp: hp, shield, burn: 0, slow: 0, slowUntil: 0};
    });
    const nodes = new Map(doc.nodes.filter(node => Object.hasOwn(Model.catalog, node.kind))
      .map(node => [node.id, {...node, params: effectiveParams(node), routes: new Map()}]));
    for (const {from, port, to} of doc.edges) {
      const parent = nodes.get(from), child = nodes.get(to);
      if (!parent || !child || child.kind === 'cast' || !Model.catalog[parent.kind].ports.some(p => p.key === port)) continue;
      if (!parent.routes.has(port)) parent.routes.set(port, []);
      parent.routes.get(port).push(child);
    }

    const events = [], jobs = [], visuals = [], states = [];
    let cursor = 0;
    const atTarget = (actor, time) => ({actor, point: position(setup, actor.target, time)});

    function emit(node, kind, label, detail, time, context, extra) {
      events.push({time, nodeId: node.id, target: context.actor?.target.id ?? null,
        kind, label, detail, ...context.point, ...extra});
    }

    function schedule(time, apply) {
      time = clock(time);
      // Equal times use scheduling order. A hit can schedule a later detonation.
      let lo = cursor, hi = jobs.length;
      while (lo < hi) {
        const mid = (lo + hi) >>> 1;
        if (jobs[mid].time <= time) lo = mid + 1;
        else hi = mid;
      }
      jobs.splice(lo, 0, {time, apply});
    }

    function visual(node, start, end, sample) {
      const result = {start: clock(start), end: clock(end), sample, node};
      visuals.push(result);
      return result;
    }

    function follow(node, port, time, context, path = new Set([node.id])) {
      for (const child of node.routes.get(port) ?? []) {
        if (path.has(child.id)) continue;
        path.add(child.id);
        execute(child, time, context, path);
        path.delete(child.id);
      }
    }

    function melee(node, time) {
      const {range, angle, windup, duration} = node.params;
      const start = time + windup, end = start + duration;
      visual(node, start, end, () => ({...CASTER, range, angle, direction, phase: 'active'}));
      let hits = 0;
      for (const actor of actors) {
        const first = firstSweep(setup, actor.target, start, end, node.params, direction);
        if (first === null) continue;
        schedule(first, t => {
          if (actor.hp <= 0) return;
          hits++;
          const target = atTarget(actor, t);
          emit(node, 'hit', '横扫命中', '目标在有效窗口内进入扇形。', t, target);
          follow(node, 'hit', t, target);
        });
      }
      schedule(end, t => {
        if (!hits) emit(node, 'miss', '未命中', '有效窗口内没有目标进入扇形。', t, {point: CASTER});
      });
    }

    function bullet(node, time) {
      const {speed, range, radius} = node.params;
      const ux = Math.cos(direction), uy = Math.sin(direction);
      const toX = ux > EPSILON ? (6 - CASTER.x) / ux : ux < -EPSILON ? (-6 - CASTER.x) / ux : Infinity;
      const toY = Math.abs(uy) > EPSILON ? 4 / Math.abs(uy) : Infinity;
      const travel = Math.min(range, toX, toY);
      const end = time + travel / speed;
      const centerAt = t => {
        const distance = clamp((t - time) * speed, 0, travel);
        return {x: CASTER.x + ux * distance, y: CASTER.y + uy * distance};
      };
      const shot = {budget: null, stopped: false, hits: 0};
      const fx = visual(node, time, end, t => ({...centerAt(t), radius, range, direction, phase: 'flight'}));
      for (const actor of actors) {
        const first = circleTimes(setup, actor.target, time, end, centerAt, radius)[0]?.[0];
        if (first === undefined) continue;
        schedule(first, t => {
          if (shot.stopped || actor.hp <= 0) return;
          shot.hits++;
          const target = atTarget(actor, t), center = centerAt(t);
          target.contact = {shot, point: target.point, nodeId: node.id, target: actor.target.id};
          emit(node, 'hit', '弹体接触', '弹体碰撞圆接触目标中心。', t, target, {bulletX: center.x, bulletY: center.y});
          follow(node, 'hit', t, target);
          if (shot.budget > 0) {
            shot.budget--;
          } else {
            shot.stopped = true;
            fx.end = t;
            fx.includeEnd = true;
          }
        });
      }
      schedule(end, t => {
        if (!shot.hits) emit(node, 'miss', '未命中', '弹体在到达射程或场地边界前没有接触。', t, {point: centerAt(t)});
      });
    }

    function blast(node, time, contact) {
      const {radius, delay} = node.params;
      const impact = clock(time + delay), center = contact?.point ?? aim;
      const origin = contact ? {triggerNodeId: contact.nodeId, triggerTarget: contact.target} : {};
      const location = {...origin, centerX: center.x, centerY: center.y, radius};
      visual(node, time, impact + 0.45, t => ({...center, radius, phase: t < impact ? 'telegraph' : 'impact'}));
      schedule(impact, t => {
        const detail = contact ? `在弹体与目标 ${contact.target} 的接触点起爆。` : '在施放瞄准点起爆。';
        emit(node, 'detonate', '起爆', detail, t, {point: center}, location);
        let hits = 0;
        for (const actor of actors) {
          if (actor.hp <= 0) continue;
          const target = atTarget(actor, t);
          if (lengthSquared(offset(target.point, center)) > radius * radius + EPSILON) continue;
          hits++;
          emit(node, 'hit', '爆炸命中', '目标在起爆半径内。', t, target, location);
          // A radius check begins a fresh target chain, even for the trigger target.
          follow(node, 'hit', t, target);
        }
        if (!hits) emit(node, 'miss', '未命中', '起爆时半径内没有存活目标。', t, {point: center}, location);
      });
    }

    function field(node, time) {
      const {radius, duration, interval} = node.params;
      const end = clock(time + duration), occupants = new Set();
      visual(node, time, end, () => ({...aim, radius, phase: 'active'}));
      for (const actor of actors) {
        for (const [entry, exit] of circleTimes(setup, actor.target, time, end, () => aim, radius)) {
          if (clock(exit) <= clock(entry)) continue;
          schedule(entry, t => {
            if (actor.hp <= 0) return;
            occupants.add(actor);
            const target = atTarget(actor, t);
            emit(node, 'enter', '进入区域', '目标进入有效区域。', t, target);
            follow(node, 'enter', t, target);
          });
          if (clock(exit) >= end) continue;
          schedule(exit, t => {
            if (!occupants.delete(actor) || actor.hp <= 0) return;
            const target = atTarget(actor, t);
            emit(node, 'exit', '离开区域', '目标越过区域边界。', t, target);
            follow(node, 'exit', t, target);
          });
        }
      }
      for (let i = 1; clock(time + i * interval) < end; i++) {
        schedule(time + i * interval, t => {
          for (const actor of actors) {
            if (!occupants.has(actor) || actor.hp <= 0) continue;
            const target = atTarget(actor, t);
            emit(node, 'tick', '区域脉冲', `第 ${i} 次周期，目标在区域内。`, t, target, {pulse: i});
            follow(node, 'tick', t, target);
          }
        });
      }
    }

    function execute(node, time, context, path) {
      const {actor, contact} = context, params = node.params;
      switch (node.kind) {
        case 'melee': if (!actor) melee(node, time); return;
        case 'bullet': if (!actor) bullet(node, time); return;
        case 'blast': if (!actor || contact) blast(node, time, contact); return;
        case 'field': if (!actor) field(node, time); return;
        case 'damage': {
          if (!actor) return;
          let damage = null;
          if (actor.hp > 0) {
            const amount = params.amount, absorbed = Math.min(actor.shield, amount);
            const hpLost = Math.min(actor.hp, amount - absorbed);
            const overkill = Math.max(0, amount - absorbed - hpLost);
            actor.shield -= absorbed;
            actor.hp -= hpLost;
            damage = {amount, absorbed, hpLost, overkill, killed: actor.hp === 0};
            const detail = `基础 ${display(amount)} · 护盾吸收 ${display(absorbed)} · 扣血 ${display(hpLost)}`;
            emit(node, 'damage', '伤害结算', detail, time, context, damage);
          }
          // A sibling or a conditional branch must never replace the caller's result.
          follow(node, 'next', time, {...context, damage}, path);
          return;
        }
        case 'gate': {
          const result = context.damage;
          if (!result) return;
          const condition = params.condition;
          const passed = condition === 'kill' ? result.killed
            : condition === 'shieldOnly' ? result.absorbed > 0 && result.hpLost === 0
              : result.hpLost > 0;
          const label = Model.catalog.gate.fields[0].options.find(option => option.value === condition).label;
          const detail = `${label} · 护盾吸收 ${display(result.absorbed)} · 扣血 ${display(result.hpLost)}`;
          emit(node, passed ? 'gate_pass' : 'gate_fail', passed ? '条件通过' : '条件未通过',
            detail, time, context, {condition, ...result});
          follow(node, passed ? 'pass' : 'fail', time, context, path);
          break;
        }
        case 'burn': {
          if (!actor) return;
          const key = params.shared;
          const def = shared && Object.hasOwn(shared, key) ? shared[key] : null;
          const addedStacks = Math.max(0, Math.floor(numeric(def?.stacks, 0)));
          if (actor.hp > 0 && addedStacks > 0) {
            actor.burn += addedStacks;
            emit(node, 'burn', '施加燃烧', `${def.name || key} +${addedStacks} 层 · 当前 ${actor.burn} 层`,
              time, context, {shared: key, addedStacks, stacks: actor.burn});
          }
          break;
        }
        case 'pierce': {
          if (!contact) return;
          // The first eligible contact sets this projectile's budget once.
          if (contact.shot.budget === null) {
            contact.shot.budget = params.count === 0 ? Infinity : params.count;
            const detail = params.count === 0 ? '可继续接触任意数量的目标。' : `允许再接触 ${params.count} 个目标。`;
            emit(node, 'pierce', '弹体穿透', detail, time, context, {count: params.count, triggerNodeId: contact.nodeId});
          }
          break;
        }
        case 'cue': {
          const style = params.style;
          const label = Model.catalog.cue.fields[0].options.find(option => option.value === style).label;
          emit(node, 'cue', '提示反馈', `${label}提示`, time, context, {style});
          break;
        }
        case 'slow': {
          if (!actor) return;
          if (actor.hp > 0 && params.percent > 0) {
            actor.slow = params.percent;
            actor.slowUntil = clock(time + params.duration);
            emit(node, 'slow', '施加减速', `减速 ${params.percent}% · 持续 ${params.duration} 秒`,
              time, context, {percent: params.percent, duration: params.duration});
          }
          break;
        }
        default: return;
      }
      follow(node, 'next', time, context, path);
    }

    const root = nodes.get('cast');
    if (root?.kind === 'cast') {
      const context = {point: CASTER};
      emit(root, 'cast', '施放', '记录本次施放的瞄准点。', 0, context, {aimX: aim.x, aimY: aim.y, direction});
      follow(root, 'start', 0, context);
    }
    const capture = time => states.push({time, actors: actors.map(actor => ({...actor}))});
    capture(0);
    while (cursor < jobs.length) {
      const time = jobs[cursor].time;
      do {
        jobs[cursor++].apply(time);
      } while (cursor < jobs.length && jobs[cursor].time === time);
      capture(time);
    }

    let duration = Math.max(1, (events.at(-1)?.time ?? 0) + 0.5);
    const times = new Set(states.map(state => state.time));
    for (const effect of visuals) {
      duration = Math.max(duration, effect.end + 0.25);
      times.add(effect.start);
      times.add(effect.end);
    }
    for (const state of states) {
      for (const {slowUntil} of state.actors) times.add(slowUntil);
    }
    for (const {hp, slowUntil} of states.at(-1).actors) {
      if (hp > 0) duration = Math.max(duration, slowUntil);
    }
    duration = clock(duration);
    times.add(duration);
    for (let i = 0; i / 60 < duration; i++) times.add(clock(i / 60));
    let stateIndex = 0;
    const frames = [...times].filter(time => time <= duration).sort((a, b) => a - b).map(time => {
      while (states[stateIndex + 1]?.time <= time) stateIndex++;
      const current = states[stateIndex].actors.map(({target, hp, maxHp, shield, burn, slow, slowUntil}) => ({
        id: target.id, ...position(setup, target, time), hp, maxHp, shield, burn, slow: hp > 0 && time < slowUntil ? slow : 0
      }));
      const fx = [];
      for (const effect of visuals) {
        if (time < effect.start || time > effect.end || (time === effect.end && !effect.includeEnd)) continue;
        fx.push({kind: effect.node.kind, nodeId: effect.node.id, ...effect.sample(time)});
      }
      return {time, actors: current, fx};
    });
    return {duration, events, frames};
  }

  window.LabSim = {run, position};
})();
