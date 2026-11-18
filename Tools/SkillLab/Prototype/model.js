(() => {
  'use strict';

  const number = (key, label, min, max, step, unit) => ({key, label, type: 'number', min, max, step, unit});
  const select = (key, label, options) => ({key, label, type: 'select', options: options.map(([value, label]) => ({value, label}))});
  const port = (key, label, context) => ({key, label, context});
  const next = () => port('next', '继续 · 沿用上下文', 'inherit');

  const catalog = {
    cast: {
      title: '施放', group: '入口', summary: '从施法者发起技能，锁定本次瞄准方向和位置。',
      fields: [], ports: [port('start', '施放时', 'cast')]
    },
    melee: {
      title: '近战横扫', group: '判定', summary: '前摇后开启扇形判定窗口，每个目标在窗口内最多命中一次。',
      fields: [number('range', '攻击距离', 0.5, 8, 0.1, '米'), number('angle', '扇形全角', 10, 360, 5, '°'),
        number('windup', '前摇', 0, 3, 0.05, '秒'), number('duration', '判定窗口', 0.05, 2, 0.05, '秒')],
      ports: [port('hit', '命中目标时', 'target')]
    },
    bullet: {
      title: '直线弹体', group: '判定', summary: '沿施法方向飞行，默认在第一次接触时停止。接触链可加穿透。',
      fields: [number('speed', '飞行速度', 1, 24, 0.5, '米/秒'), number('range', '最大射程', 1, 12, 0.5, '米'),
        number('radius', '碰撞半径', 0.1, 1.5, 0.05, '米')],
      ports: [port('hit', '接触目标时', 'contact')]
    },
    blast: {
      title: '定点爆炸', group: '判定', summary: '锁定瞄准点或弹体接触点，延迟后对圆内目标各判定一次。',
      fields: [number('radius', '爆炸半径', 0.25, 6, 0.05, '米'), number('delay', '起爆延迟', 0, 4, 0.05, '秒')],
      ports: [port('hit', '爆炸命中时', 'target')]
    },
    field: {
      title: '持续区域', group: '判定', summary: '在瞄准点生成区域，分别触发进入、周期和离开。',
      fields: [number('radius', '区域半径', 0.25, 6, 0.05, '米'), number('duration', '持续时间', 0.5, 12, 0.1, '秒'),
        number('interval', '周期间隔', 0.1, 6, 0.05, '秒')],
      ports: [port('enter', '进入区域时', 'target'), port('tick', '区内周期', 'target'), port('exit', '离开区域时', 'target')]
    },
    damage: {
      title: '伤害结算', group: '效果', summary: '先消耗护盾，再扣生命。下游可读取这一次的独立伤害结果。',
      fields: [number('amount', '基础伤害', 0, 500, 1, '点')],
      ports: [port('next', '结算后 · 携带伤害结果', 'damage')]
    },
    gate: {
      title: '伤害后条件', group: '条件', summary: '检查本次伤害结果，执行符合或不符合分支，再执行无条件继续。',
      fields: [select('condition', '通过条件', [['hpLost', '本次实际扣血'], ['shieldOnly', '本次仅消耗护盾'], ['kill', '本次击倒']])],
      ports: [port('pass', '条件通过', 'inherit'), port('fail', '条件未通过', 'inherit'), next()]
    },
    burn: {
      title: '施加燃烧', group: '效果', summary: '按引用的共享定义增加目标燃烧层数，副本可以独立编辑。',
      fields: [select('shared', '引用定义', [['burn', '燃烧']])], ports: [next()]
    },
    pierce: {
      title: '弹体穿透', group: '效果', summary: '在接触结算后允许同一颗弹体继续飞行；0 表示不限额外命中数。',
      fields: [number('count', '额外命中数', 0, 10, 1, '次')], ports: [next()]
    },
    cue: {
      title: '提示反馈', group: '表现', summary: '在施法点或目标处记录提示，可继续原来的执行链。',
      fields: [select('style', '提示样式', [['impact', '冲击'], ['spark', '火花'], ['pulse', '脉冲']])], ports: [next()]
    },
    slow: {
      title: '施加减速', group: '效果', summary: '为目标记录有时限的减速状态；试验场移动路线保持固定。',
      fields: [number('percent', '减速比例', 0, 100, 5, '%'), number('duration', '状态时长', 0.1, 8, 0.1, '秒')], ports: [next()]
    }
  };

  const defaults = {
    cast: {}, melee: {range: 3.5, angle: 100, windup: 0.35, duration: 0.35},
    bullet: {speed: 8, range: 10, radius: 0.4}, blast: {radius: 2.25, delay: 0.7},
    field: {radius: 2.25, duration: 4.8, interval: 0.75}, damage: {amount: 34}, gate: {condition: 'hpLost'},
    burn: {shared: 'burn'}, pierce: {count: 1}, cue: {style: 'impact'}, slow: {percent: 35, duration: 1.3}
  };

  const templates = [
    {id: 'melee', title: '近战横扫', summary: '比较前摇、动作窗口与扇形覆盖。', icon: '斩'},
    {id: 'bullet', title: '直线子弹', summary: '比较首次接触停止与追加穿透。', icon: '弹'},
    {id: 'blast', title: '爆裂火球', summary: '弹体接触后引爆，比较护盾吸收、实际扣血和燃烧。', icon: '爆'},
    {id: 'field', title: '持续火圈', summary: '跟踪往返移动目标的进出和区内周期。', icon: '环'},
    {id: 'blank', title: '空白技能', summary: '从施放入口开始，选一种判定并接上效果。', icon: '+'}
  ];

  function makeNode(kind, id = kind) {
    if (!Object.hasOwn(defaults, kind)) throw new RangeError(`未知节点类型：${kind}`);
    return {id, kind, params: {...defaults[kind]}};
  }

  function create(id) {
    const template = templates.find(template => template.id === id) ?? templates.at(-1);
    const doc = {templateId: template.id, title: template.title, nodes: [makeNode('cast')], edges: []};
    function append(from, port, kind, params) {
      const node = makeNode(kind);
      Object.assign(node.params, params);
      doc.nodes.push(node);
      doc.edges.push({from, port, to: node.id});
    }
    if (template.id === 'blank') return doc;
    if (template.id === 'blast') {
      append('cast', 'start', 'bullet');
      append('bullet', 'hit', 'blast');
    } else {
      append('cast', 'start', template.id);
    }
    append(template.id, template.id === 'field' ? 'tick' : 'hit', 'damage', {
      amount: {melee: 26, bullet: 32, blast: 34, field: 12}[template.id]
    });
    if (template.id === 'bullet') return doc;
    append('damage', 'next', 'gate');
    append('gate', 'pass', 'burn');
    if (template.id === 'blast') append('gate', 'fail', 'cue', {style: 'spark'});
    if (template.id === 'field') append('field', 'enter', 'slow');
    return doc;
  }

  function setup(id) {
    const initial = {
      melee: {aim: [1.5, 0], a: [-1.6, -0.45, 0], b: [-1.6, 1.75, 30]},
      bullet: {aim: [5, 0], a: [-0.6, 0, 0], b: [3.1, 0, 0]},
      blast: {aim: [4.6, 0], a: [1.2, 0, 0], b: [2.6, 0.95, 50]},
      field: {aim: [1, 0], a: [0.6, -0.55, 0], b: [1.6, 3.4, 22]},
      blank: {aim: [0, 0], a: [-1.5, 0, 0], b: [1.6, 1.1, 20]}
    };
    const {aim, a, b} = Object.hasOwn(initial, id) ? initial[id] : initial.blank;
    const target = (id, [x, y, shield]) => ({id, x, y, hp: 100, shield});
    return {aim: {x: aim[0], y: aim[1]}, targets: [target('A', a), target('B', b)], path: id === 'field' ? 'cross' : 'still'};
  }

  const sharedDefaults = () => ({burn: {name: '燃烧', stacks: 1}});
  const delivery = new Set(['melee', 'bullet', 'blast', 'field']);
  const TARGET = 1, CONTACT = 2, DAMAGE = 4;

  function outputContext(context, input) {
    switch (context) {
      case 'cast': return 0;
      case 'target': return TARGET;
      case 'contact': return TARGET | CONTACT;
      case 'damage': return input | DAMAGE;
      default: return input;
    }
  }

  function restriction(kind, context) {
    if (kind === 'cast') return '每个技能只有一个施放入口。';
    if (kind === 'cue') return '';
    if (kind === 'blast') return (context & TARGET) && !(context & CONTACT) ? '爆炸需要施放或弹体接触上下文。' : '';
    if (delivery.has(kind)) return context & TARGET ? '判定节点需要施放上下文，请从施放链添加。' : '';
    if (kind === 'pierce') return context & CONTACT ? '' : '穿透需要弹体接触上下文，请从弹体「接触目标时」或其后续链添加。';
    if (kind === 'gate') return context & DAMAGE ? '' : '条件需要本次伤害结果，请接在「伤害结算」之后。';
    return context & TARGET ? '' : '此效果需要目标，请接在命中、接触或区域事件之后。';
  }

  function choices(doc, parentId, portKey) {
    const nodes = new Map(doc.nodes.map(node => [node.id, node]));
    const outgoing = new Map();
    for (const edge of doc.edges) {
      if (!outgoing.has(edge.from)) outgoing.set(edge.from, []);
      outgoing.get(edge.from).push(edge);
    }
    const contexts = new Set(), visited = new Map();
    const pending = nodes.get('cast')?.kind === 'cast' ? [['cast', 0]] : [];
    while (pending.length) {
      const [id, input] = pending.pop();
      if (!visited.has(id)) visited.set(id, new Set());
      if (visited.get(id).has(input)) continue;
      visited.get(id).add(input);
      const ports = catalog[nodes.get(id).kind]?.ports ?? [];
      for (const output of ports) {
        const context = outputContext(output.context, input);
        if (id === parentId && output.key === portKey) contexts.add(context);
        for (const edge of outgoing.get(id) ?? []) {
          const child = nodes.get(edge.to);
          if (edge.port === output.key && child && Object.hasOwn(catalog, child.kind) && !restriction(child.kind, context)) {
            pending.push([child.id, context]);
          }
        }
      }
    }
    return Object.keys(catalog).map(kind => {
      const reason = contexts.size
        ? Array.from(contexts, context => restriction(kind, context)).find(Boolean) ?? ''
        : '此端口未连接到有效的施放路径。';
      return {kind, enabled: !reason, reason};
    });
  }

  window.LabModel = {templates, catalog, create, makeNode, choices, sharedDefaults, setup};
})();
