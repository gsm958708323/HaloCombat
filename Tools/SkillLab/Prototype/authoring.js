(() => {
  'use strict';

  const MIN_ZOOM = .65, MAX_ZOOM = 1.6, SVG = 'http://www.w3.org/2000/svg';
  const glyphs = {
    graph: 'M3 3h5v5H3zM16 16h5v5h-5zM8 5.5h6a4.5 4.5 0 0 1 4.5 4.5v6M6 8v10h6',
    cast: 'M13 2 5 13h6l-1 9 9-12h-6z', target: 'M12 2v4m0 12v4M2 12h4m12 0h4M19 12a7 7 0 1 1-14 0 7 7 0 0 1 14 0Z',
    melee: 'm4 20 3-11a19 19 0 0 1 12 9L4 20Zm0 0L17 6M14 5l4-2 2 4',
    bullet: 'm4 16 5-2m-3 6 4-2m2-4 2-5 7-4-3 8-5 2z',
    blast: 'm12 2 2.5 7.5L22 12l-7.5 2.5L12 22l-2.5-7.5L2 12l7.5-2.5z',
    field: 'M3 16a9 4 0 1 0 18 0 9 4 0 0 0-18 0Zm4-6 2-4m3 3 1-6m3 7 3-5',
    gate: 'M12 2 22 12 12 22 2 12zM8 12h8m-4-4v8',
    cue: 'm12 2 1.8 7.2L21 11l-7.2 1.8L12 20l-1.8-7.2L3 11l7.2-1.8zM19 3v4m-2-2h4',
    pierce: 'M3 12h18m-5-5 5 5-5 5M10 5v4m0 6v4', slow: 'M19 12a7 7 0 1 1-14 0 7 7 0 0 1 14 0Zm-7-4v4l2 2',
    damage: 'M4 12h5l2-6 3 12 2-6h4', burn: 'M12 2c2 5-2 7 3 10 2-1 3-3 3-4 5 8 1 13-6 13-6 0-9-4-6-10 0 3 2 4 3 4-2-6 3-8 3-13Z',
    fit: 'M9 4H4v5m11-5h5v5M4 15v5h5m11-5v5h-5M9 9h6v6H9z',
    search: 'M16 10a6 6 0 1 1-12 0 6 6 0 0 1 12 0Zm-2 5 6 6',
    trash: 'M4 6h16m-14 0 1 14h10l1-14M9 6V3h6v3m-5 4v6m4-6v6'
  };
  const tones = {cast:'green', target:'green', melee:'amber', bullet:'blue', blast:'amber',
    field:'violet', damage:'amber', gate:'violet', burn:'amber', pierce:'blue', cue:'blue', slow:'violet'};
  const state = {zoom:1, autoFit:true, cards:new Map(), refresh:[]};
  const model = () => window.LabModel;
  const meta = kind => model().catalog[kind] ?? {title:kind, group:'节点', fields:[], ports:[]};
  const isInput = node => window.LabGraphLayout.isInput(node);
  const eventPort = port => window.LabGraphLayout.isEventPort(port);
  const clamp = (n, lo, hi) => Math.max(lo, Math.min(hi, n));
  function el(tag, className, ...children) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    element.append(...children.filter(child => child != null));
    return element;
  }
  function icon(name) {
    const svg = document.createElementNS(SVG, 'svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('aria-hidden', 'true');
    const path = document.createElementNS(SVG, 'path');
    path.setAttribute('d', glyphs[name] || glyphs.graph);
    svg.append(path);
    return svg;
  }
  function button(text, fn, className, label) {
    const control = el('button', className, text);
    control.type = 'button';
    control.onclick = fn;
    if (label) { control.title = label; control.setAttribute('aria-label', label); }
    return control;
  }
  function themed(element, kind) {
    element.dataset.tone = tones[kind] ?? 'green';
    return element;
  }
  function glyph(kind) {
    return themed(el('span', 'la-glyph', icon(kind)), kind);
  }

  function establish(graph, inspector) {
    const smaller = button('−', () => zoomBy(-.15), '', '缩小');
    const scale = button('100%', () => zoomBy(null), 'la-scale', '恢复 100%');
    const larger = button('+', () => zoomBy(.15), '', '放大');
    const fit = button('', fitGraph, 'la-fit', '适应画布');
    fit.append(icon('fit'));
    const count = el('span', 'la-count');
    const head = el('div', 'la-bar',
      el('div', 'la-heading', icon('graph'), el('h2', '', '技能画布'), count),
      el('div', 'la-tools', smaller, scale, larger, fit));
    const scene = el('div', 'la-scene'), plane = el('div', 'la-plane', scene);
    const viewport = el('div', 'la-viewport', el('div', 'la-scroll', plane));
    viewport.tabIndex = 0;
    viewport.setAttribute('aria-label', '技能节点图');
    const legend = el('div', 'la-legend', el('span', 'la-legend-flow', '执行'), el('span', 'la-legend-event', '事件'),
      el('span', 'la-gesture', '拖动空白处平移'));
    const dialog = el('dialog', 'la-dialog');
    dialog.setAttribute('aria-labelledby', 'la-dialog-title');
    const body = el('div', 'la-inspector-body');
    graph.replaceChildren(head, viewport, legend, dialog);
    inspector.replaceChildren(el('div', 'la-bar', el('h2', '', '节点属性')), body);
    Object.assign(state, {graph, inspector, viewport, plane, scene, count, scale, smaller, larger, body, dialog});
    graph.onkeydown = inspector.onkeydown = handleDeleteKey;
    bindPan(viewport);
    state.observer?.disconnect();
    state.observer = new ResizeObserver(() => { if (state.autoFit && state.diagram) fitGraph(); });
    state.observer.observe(viewport);
    state.shape = state.panel = null;
  }

  function bindPan(view) {
    let drag;
    view.onpointerdown = event => {
      if (event.button !== 0 || event.pointerType !== 'mouse' || event.target.closest('button')) return;
      if (view.scrollWidth <= view.clientWidth && view.scrollHeight <= view.clientHeight) return;
      drag = {x:event.clientX, y:event.clientY, left:view.scrollLeft, top:view.scrollTop};
      view.focus({preventScroll:true});
      view.setPointerCapture(event.pointerId);
      view.classList.add('is-panning');
    };
    view.onpointermove = event => {
      if (drag) view.scrollTo(drag.left + drag.x - event.clientX, drag.top + drag.y - event.clientY);
    };
    view.onlostpointercapture = () => { drag = null; view.classList.remove('is-panning'); };
  }
  function applyScale(zoom) {
    const {diagram, plane, scene, scale, smaller, larger} = state;
    state.zoom = zoom;
    plane.style.width = `${Math.ceil(diagram.width * zoom)}px`;
    plane.style.height = `${Math.ceil(diagram.height * zoom)}px`;
    scene.style.transform = `scale(${zoom})`;
    scale.textContent = `${Math.round(zoom * 100)}%`;
    smaller.disabled = zoom <= MIN_ZOOM;
    larger.disabled = zoom >= MAX_ZOOM;
  }
  function fitGraph() {
    const {viewport, diagram} = state;
    state.autoFit = true;
    const available = Math.min((viewport.clientWidth - 28) / diagram.width, (viewport.clientHeight - 36) / diagram.height);
    applyScale(clamp(available, MIN_ZOOM, 1));
  }
  function zoomBy(delta) {
    const {viewport, plane, zoom} = state;
    const rect = viewport.getBoundingClientRect(), old = plane.getBoundingClientRect();
    const cx = rect.left + viewport.clientWidth / 2, cy = rect.top + viewport.clientHeight / 2;
    state.autoFit = false;
    applyScale(delta == null ? 1 : clamp(zoom + delta, MIN_ZOOM, MAX_ZOOM));
    const next = plane.getBoundingClientRect(), ratio = state.zoom / zoom;
    viewport.scrollBy(next.left + (cx - old.left) * ratio - cx, next.top + (cy - old.top) * ratio - cy);
  }
  function reveal(id, focus = false) {
    const card = state.cards.get(id);
    if (!card) return;
    const frame = state.viewport.getBoundingClientRect(), rect = card.root.getBoundingClientRect();
    const dx = rect.left < frame.left + 7 ? rect.left - frame.left - 7 : Math.max(0, rect.right - frame.right + 12);
    const dy = rect.top < frame.top + 7 ? rect.top - frame.top - 7 : Math.max(0, rect.bottom - frame.bottom + 12);
    if (dx || dy) state.viewport.scrollBy(dx, dy);
    if (focus) card.control.focus({preventScroll:true});
  }

  function portButton(node, port, className, ...label) {
    const open = button('', () => showPicker(node.id, port), className,
      `从${meta(node.kind).title}的「${port.label}」添加节点`);
    open.classList.toggle('is-event', eventPort(port));
    open.append(...label, el('span', 'la-port-plus', '+'));
    return open;
  }
  function drawCard(box) {
    const {node, x, y, ports, width, height} = box, info = meta(node.kind);
    const root = themed(el('div', 'la-node'), node.kind);
    root.style.left = `${x}px`;
    root.style.top = `${y}px`;
    root.style.width = `${width}px`;
    root.style.height = `${height}px`;
    root.dataset.nodeId = node.id;
    root.classList.toggle('has-input', !!box.incoming);
    root.classList.toggle('is-side-input', !!box.sideInput);
    const control = button('', () => state.api.select(node.id), 'la-node-main');
    control.setAttribute('aria-label', info.title);
    const label = el('span', 'la-node-label',
      el('small', '', isInput(node) ? '输入' : info.group), el('strong', '', info.title));
    const readout = el('span', 'la-node-reading');
    control.append(glyph(node.kind), label, readout);
    root.append(control);
    if (ports.length) {
      const strip = el('div', 'la-port-strip');
      strip.style.gridTemplateColumns = `repeat(${ports.length}, minmax(0, 1fr))`;
      strip.classList.toggle('is-stacked', ports.length >= 3);
      ports.forEach((port, i) => {
        const caption = port.label.split(' · ')[0];
        const open = portButton(node, port, 'la-port', el('span', 'la-port-label', caption));
        open.classList.toggle('is-linked', box.out.some(edge => edge.port === i));
        open.classList.toggle('is-side-port', box.out.some(edge => edge.port === i && edge.side));
        strip.append(open);
      });
      root.append(strip);
    }
    state.cards.set(node.id, {root, control, readout});
    return root;
  }
  function drawGraph() {
    const {width, height, face, cards, wires, starter} = state.diagram;
    state.cards.clear();
    const ink = document.createElementNS(SVG, 'svg');
    ink.setAttribute('viewBox', `0 0 ${width} ${height}`);
    ink.setAttribute('aria-hidden', 'true');
    ink.classList.add('la-wires');
    for (const wire of wires) {
      const path = document.createElementNS(SVG, 'path');
      path.classList.add('la-wire', `is-${wire.kind}`);
      path.setAttribute('d', wire.path);
      ink.append(path);
    }
    state.scene.style.width = `${width}px`;
    state.scene.style.height = `${height}px`;
    state.scene.style.setProperty('--la-face', `${face}px`);
    state.scene.replaceChildren(ink, ...cards.map(drawCard));
    if (starter) {
      const add = portButton(starter.node, starter.port, 'la-start', el('span', '', '添加首个节点'));
      Object.assign(add.style, {top:`${starter.y}px`, left:`${starter.x}px`, width:`${starter.width}px`});
      state.scene.append(add);
    }
    if (!cards.length) state.scene.append(el('p', 'la-empty', '暂无节点'));
  }
  function fieldText(field, value) {
    if (field.type === 'select') return field.options?.find(opt => opt.value === value)?.label ?? value;
    return `${value}${field.unit ? ` ${field.unit}` : ''}`;
  }
  function updateCards() {
    const {doc, selected, selectedEvent, shared} = state.api;
    for (const node of doc.nodes) {
      const {root, control, readout} = state.cards.get(node.id);
      control.setAttribute('aria-pressed', String(selected === node.id));
      root.classList.toggle('is-selected', selected === node.id);
      root.classList.toggle('is-located', selectedEvent?.nodeId === node.id);
      const field = meta(node.kind).fields.find(item => node.params[item.key] != null);
      const burn = node.kind === 'burn' && shared[node.params.shared || 'burn'];
      const line = burn ? `每次叠加 ${burn.stacks} 层` : field ? `${field.label} ${fieldText(field, node.params[field.key])}` : meta(node.kind).summary;
      readout.textContent = line;
    }
  }

  function openDialog(title, subtitle) {
    const {dialog} = state;
    const heading = el('h2', '', title);
    heading.id = 'la-dialog-title';
    const words = el('div', '', el('p', 'la-context', subtitle), heading);
    dialog.replaceChildren(el('div', 'la-dialog-head', words, button('×', () => dialog.close(), 'la-close', '关闭')));
    return dialog;
  }
  function showPicker(id, port) {
    const {doc} = state.api, parent = doc.nodes.find(node => node.id === id);
    const choices = model().choices(doc, id, port.key);
    const dialog = openDialog('添加节点', `${meta(parent.kind).title} / ${port.label}`);
    const search = el('input');
    search.type = 'search';
    search.placeholder = '搜索名称或类型';
    search.setAttribute('aria-label', '搜索节点');
    const results = el('div', 'la-results');
    dialog.append(el('label', 'la-search', icon('search'), search), results);
    function populate() {
      const query = search.value.trim().toLowerCase();
      const matches = choices.filter(({kind}) => {
        const {title, group} = meta(kind);
        return `${title} ${group} ${kind}`.toLowerCase().includes(query);
      });
      results.replaceChildren();
      results.scrollTop = 0;
      for (const enabled of [true, false]) {
        const section = matches.filter(choice => choice.enabled === enabled);
        if (!section.length) continue;
        results.append(el('h3', 'la-results-label', `${enabled ? '可添加' : '当前不可用'} · ${section.length}`));
        for (const {kind, reason} of section) {
          const {title, group, summary} = meta(kind);
          const choice = button('', () => {
            dialog.close();
            state.api.addNode(id, port.key, kind);
            reveal(state.api.selected, true);
          }, 'la-choice');
          choice.disabled = !enabled;
          const text = el('span', 'la-choice-text', el('strong', '', title),
            el('small', '', enabled ? summary : reason || '此端口不支持该节点'));
          choice.append(glyph(kind), text, el('span', 'la-group', group));
          results.append(choice);
        }
      }
      if (!matches.length) results.append(el('p', 'la-empty', query ? '没有符合搜索的节点' : '此端口没有可选节点'));
    }
    search.oninput = populate;
    dialog.onkeydown = event => {
      if (event.isComposing) return;
      if (event.key === 'Escape') {
        event.preventDefault();
        return dialog.close();
      }
      const items = [...results.querySelectorAll('button:enabled')], i = items.indexOf(event.target);
      if (event.key === 'Enter' && event.target === search) {
        event.preventDefault();
        items[0]?.click();
      } else if ((event.target === search || i >= 0) && ['ArrowDown', 'ArrowUp'].includes(event.key)) {
        event.preventDefault();
        const next = event.key === 'ArrowDown' ? i + 1 : i < 0 ? items.length - 1 : i - 1;
        (items[next] || search).focus();
      }
    };
    populate();
    dialog.showModal();
    search.focus();
  }

  function reachable(seeds, excluded) {
    const visited = new Set(), queue = [...seeds];
    while (queue.length) {
      const box = queue.pop();
      if (box === excluded || visited.has(box)) continue;
      visited.add(box);
      for (const edge of box.out) queue.push(edge.to);
    }
    return visited;
  }
  function requestDelete(id) {
    const {boxes, roots} = state.diagram, box = boxes.get(id);
    if (!box || isInput(box.node) || state.dialog.open) return;
    const kept = reachable(roots, box);
    const lost = [...reachable([box])].filter(child => child !== box && !kept.has(child));
    const dialog = openDialog(`删除「${meta(box.node.kind).title}」？`, '删除节点');
    dialog.onkeydown = null;
    dialog.append(el('p', 'la-warning', lost.length ? `将一并删除 ${lost.length} 个下游节点。` : '将移除此节点及其连线。'));
    if (lost.length) {
      const names = lost.slice(0, 4).map(item => meta(item.node.kind).title).join('、');
      dialog.append(el('p', 'la-context', lost.length > 4 ? `${names} 等` : names));
    }
    const actions = el('div', 'la-actions');
    const cancel = button('取消', () => dialog.close());
    actions.append(cancel, button('确认删除', () => {
      dialog.close();
      state.api.removeNode(id);
      reveal(state.api.selected, true);
    }, 'la-danger'));
    dialog.append(actions);
    dialog.showModal();
    cancel.focus();
  }
  function handleDeleteKey(event) {
    if (event.key !== 'Delete' || event.repeat || event.altKey || event.ctrlKey || event.metaKey) return;
    if (event.target.closest('input, select, textarea, dialog, [contenteditable], .la-port, .la-output')) return;
    event.preventDefault();
    requestDelete(event.target.closest('.la-node')?.dataset.nodeId || state.api.selected);
  }

  function parameter(field, read, save, explicit = false) {
    const number = field.type === 'number', control = el(number ? 'input' : 'select');
    const error = el('small', 'la-invalid');
    error.setAttribute('aria-live', 'polite');
    control.setAttribute('aria-label', field.unit ? `${field.label}（${field.unit}）` : field.label);
    if (number) {
      control.type = 'number'; control.required = true;
      control.step = field.step ?? 'any';
      if (field.min != null) control.min = field.min;
      if (field.max != null) control.max = field.max;
    } else control.append(...(field.options ?? []).map(opt => new Option(opt.label, opt.value)));
    function clearError() { error.textContent = ''; control.removeAttribute('aria-invalid'); }
    function restore(value) {
      const text = String(value ?? '');
      if (number && text && control.valueAsNumber === Number(text)) return;
      if (control.value !== text) control.value = text;
    }
    function commit() {
      const value = number ? control.valueAsNumber : field.options?.[control.selectedIndex]?.value;
      if (!control.validity.valid) {
        const validity = control.validity;
        error.textContent = validity.rangeUnderflow ? `最小值为 ${field.min}`
          : validity.rangeOverflow ? `最大值为 ${field.max}`
          : validity.stepMismatch ? `请按 ${field.step} 的步长输入` : '请输入有效数字';
        control.setAttribute('aria-invalid', 'true');
      } else if (value !== undefined && !Object.is(value, read())) save(value);
    }
    let baseline = Symbol();
    state.refresh.push(() => {
      const value = read();
      if (!Object.is(value, baseline)) { baseline = value; restore(value); clearError(); }
    });
    control.oninput = clearError;
    if (!explicit) control.onchange = commit;
    control.onkeydown = event => {
      if (event.isComposing) return;
      if (event.key === 'Escape' && number) { restore(read()); clearError(); }
      if (event.key === 'Enter' && number && !explicit) { event.preventDefault(); commit(); }
    };
    const wrap = el('span', 'la-control', control, field.unit ? el('span', 'la-unit', field.unit) : null);
    const row = el('div', 'la-field', el('label', '', el('span', '', field.label), wrap), error);
    return {row, commit};
  }

  function section(title) {
    return el('section', 'la-section', el('h3', '', title));
  }
  function sharedSection(id, key, shared) {
    const part = section('燃烧定义'), card = themed(el('div', 'la-shared'), 'burn');
    part.append(card);
    const definition = () => state.api.shared[key];
    if (!definition()) { card.append(el('p', 'la-context', '燃烧定义不可用')); return part; }
    const name = el('strong'), names = el('p', 'la-context');
    const head = el('div', 'la-shared-head', name, el('span', 'la-tag', shared ? '共享' : '私有'));
    state.refresh.push(() => {
      name.textContent = definition()?.name;
      const titles = shared ? state.api.refs(key) : [];
      names.textContent = shared ? `${titles.length} 个技能引用${titles.length ? ` · ${titles.join('、')}` : ''}` : '仅此节点使用';
    });
    const form = el('form');
    form.noValidate = true;
    const field = {label:'每次叠加', type:'number', min:1, max:99, step:1, unit:'层'};
    const {row, commit} = parameter(field, () => definition()?.stacks, value => {
      state.api.modifyShared(id, value);
      state.api.toast(shared ? '已更新共享燃烧' : '已保存私有燃烧');
    }, true);
    form.onsubmit = event => { event.preventDefault(); commit(); };
    const actions = el('div', 'la-actions');
    if (shared) actions.append(button('转为私有', () => {
      state.api.forkShared(id);
      state.body.querySelector('.la-shared input')?.focus();
      state.api.toast('已转为私有燃烧');
    }));
    const save = el('button', 'la-primary', shared ? '更新共享' : '保存层数');
    save.type = 'submit';
    actions.append(save);
    form.append(row, actions);
    card.append(head, names, form);
    return part;
  }
  function eventDetails() {
    const event = state.api.selectedEvent, zone = el('div', 'la-event');
    if (!event) return zone;
    const first = el('div', 'la-event-head', el('span', '', '已定位事件'),
      el('time', '', Number.isFinite(event.time) ? `${event.time.toFixed(2)} s` : ''));
    zone.append(first, el('strong', '', `${event.label || event.kind || '事件'}${event.target ? ` · ${event.target}` : ''}`));
    if (event.detail) zone.append(el('p', 'la-context', event.detail));
    if (event.nodeId && !state.cards.has(event.nodeId)) zone.append(el('p', 'la-context', '此节点已从草稿移除'));
    else if (event.nodeId && state.api.selected !== event.nodeId) zone.append(button('查看对应节点', () => {
      state.api.select(event.nodeId);
      reveal(event.nodeId, true);
    }, 'la-event-link'));
    return zone;
  }
  function renderInspector(node) {
    const {api, body} = state;
    const key = node?.kind === 'burn' ? node.params.shared || 'burn' : '';
    const shared = key && (key === 'burn' || api.refs(key).length > 1
      || api.doc.nodes.filter(item => item.kind === 'burn' && item.params.shared === key).length > 1);
    const stamp = JSON.stringify([api.doc.templateId, node?.id, node?.kind, key, !!api.shared[key], shared]);
    if (stamp !== state.panel) {
      state.panel = stamp;
      state.refresh = [];
      state.eventSlot = el('div', 'la-event-slot');
      body.replaceChildren(state.eventSlot);
      if (!node) body.append(el('p', 'la-empty', '选择画布上的节点查看属性'));
      else drawInspector(node, key, shared);
    }
    const eventStamp = JSON.stringify([stamp, api.selectedEvent, state.shape]);
    if (eventStamp !== state.eventStamp) {
      state.eventSlot.replaceChildren(...(api.selectedEvent ? [eventDetails()] : []));
      state.eventStamp = eventStamp;
    }
    state.refresh.forEach(update => update());
  }
  function drawInspector(node, key, shared) {
    const {body} = state, info = meta(node.kind);
    const title = el('div', '', el('small', '', info.group), el('h3', '', info.title));
    body.append(el('div', 'la-identity', glyph(node.kind), title));
    if (info.summary) body.append(el('p', 'la-summary', info.summary));
    const fields = info.fields.filter(field => !key || !['stacks', 'shared'].includes(field.key));
    if (fields.length) {
      const part = section('参数');
      for (const field of fields) part.append(parameter(field,
        () => state.api.doc.nodes.find(item => item.id === node.id)?.params[field.key],
        value => state.api.setParam(node.id, field.key, value)).row);
      body.append(part);
    }
    if (key) body.append(sharedSection(node.id, key, shared));
    if (info.ports.length) {
      const part = section('输出端口');
      for (const port of info.ports) {
        const count = el('small', 'la-link-count');
        state.refresh.push(() => {
          const links = state.api.doc.edges.filter(edge => edge.from === node.id && edge.port === port.key).length;
          count.textContent = links ? `${links} 条连线` : '未连接';
        });
        part.append(portButton(node, port, 'la-output', el('span', 'la-output-pin'), el('span', '', port.label), count));
      }
      body.append(part);
    }
    if (!isInput(node)) {
      const remove = button('', () => requestDelete(node.id), 'la-remove');
      remove.append(icon('trash'), '删除节点');
      body.append(el('div', 'la-inspector-foot', remove, el('kbd', '', 'Delete')));
    }
  }

  function render(api) {
    const graph = document.getElementById('graph'), inspector = document.getElementById('inspector');
    if (!graph || !inspector) return;
    const switchTemplate = state.api?.doc.templateId !== api.doc.templateId;
    const locate = state.api?.selected !== api.selected || state.api?.selectedEvent !== api.selectedEvent || switchTemplate;
    state.api = api;
    if (state.graph !== graph || state.inspector !== inspector) establish(graph, inspector);
    const shape = JSON.stringify([api.doc.templateId, api.doc.nodes.map(({id, kind}) => [id, kind]), api.doc.edges]);
    if (state.shape !== shape) {
      if (state.dialog.open) state.dialog.close();
      state.shape = shape;
      state.diagram = window.LabGraphLayout.layout(api.doc, model().catalog);
      drawGraph();
      state.count.textContent = `${api.doc.nodes.length} 节点 · ${api.doc.edges.length} 连线`;
      if (switchTemplate || state.autoFit) fitGraph();
      else applyScale(state.zoom);
      if (switchTemplate) state.viewport.scrollTo(0, 0);
    }
    updateCards();
    renderInspector(api.doc.nodes.find(node => node.id === api.selected));
    if (locate) reveal(api.selected);
  }
  window.LabAuthor = {render};
})();
