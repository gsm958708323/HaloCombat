(() => {
  'use strict';

  const WIDTH = 300, HEIGHT = 216, METER = 21.5, CASTER = {x: -4, y: 0};
  const TAU = Math.PI * 2, LIMITS = {x: 6, y: 4};
  const bound = (v, min, max) => Math.min(max, Math.max(min, v));
  const xOnMap = x => WIDTH / 2 + x * METER;
  const yOnMap = y => HEIGHT / 2 - y * METER;
  const seconds = time => `${time.toFixed(2)} s`;
  const percent = (time, duration) => `${bound(time / (duration || 1), 0, 1) * 100}%`;
  const tones = {windup: '前摇', active: '有效窗', flight: '弹体飞行', field: '火圈持续', pulse: '区域脉冲', impact: '判定事件'};
  const eventTones = {hit: 'impact', miss: 'impact', detonate: 'impact', tick: 'pulse', enter: 'field', exit: 'field'};
  const speedOptions = [.25, .5, 1, 1.5, 2, 4];
  let ui, api, draftKey, mode = 'record', subject = 'aim', drag, scrub, lastScene, lastDraft;

  function mount(root) {
    root.innerHTML = `
      <div class="ls-head"><h3>试验场</h3><div class="ls-switch" role="group" aria-label="试验场视图">
        <button type="button" data-view="draft">布置</button><button type="button" data-view="record">回放</button>
      </div></div>
      <div class="ls-body">
        <section aria-label="俯视预览">
          <canvas class="ls-map" width="${WIDTH}" height="${HEIGHT}" role="img" aria-label="俯视试验场，完整范围为 12 × 8 米"></canvas>
          <div class="ls-field-note"><span data-live="source"></span><span>俯视 · 12 × 8 m</span></div>
          <div class="ls-vitals">${['A', 'B'].map(id => `
            <div class="ls-vital" data-actor="${id}">
              <div class="ls-vital-head"><strong>目标 ${id}</strong><b data-live="hp"></b></div>
              <div class="ls-hp" aria-hidden="true"><i></i></div>
              <div class="ls-effects"><span data-live="shield"></span><span class="ls-burn" data-live="burn"></span></div>
            </div>`).join('')}</div>
          <section class="ls-timeline" aria-label="试验时间条">
            <div class="ls-transport">
              <button type="button" class="ls-play"><i class="ls-play-icon" aria-hidden="true"></i><span></span></button>
              <button type="button" class="ls-step" title="单步前进"><i class="ls-step-icon" aria-hidden="true"></i>单步</button>
              <label class="ls-rate"><span class="ls-sr">播放速度</span><select>${speedOptions.map(v => `<option value="${v}">${v}×</option>`).join('')}</select></label>
              <output class="ls-time"></output>
            </div>
            <div class="ls-axis"><div class="ls-bands" aria-hidden="true"></div>
              <input class="ls-scrub" type="range" min="0" step="0.01" aria-label="回放时间">
              <div class="ls-pins" aria-label="事件标记"></div>
            </div>
            <div class="ls-key"></div>
            <select class="ls-jump" aria-label="定位时间条事件"><option value="">定位事件…</option></select>
          </section>
        </section>
        <section class="ls-setup" aria-labelledby="ls-setup-title">
          <div class="ls-setup-title"><h4 id="ls-setup-title">初始布置</h4>
            <div class="ls-switch ls-route" role="group" aria-label="目标 B 路径"><span aria-hidden="true">B 路径</span>
              <button type="button" data-path="still">静止</button><button type="button" data-path="cross">往返</button>
            </div>
          </div>
          <p class="ls-setup-hint">布置模式下拖动 A / B，点击场地调整瞄准点。</p>
          <div class="ls-coords">
            <label>布置对象<select class="ls-subject"><option value="aim">瞄准点</option><option value="A">目标 A</option><option value="B">目标 B</option></select></label>
            ${Object.entries(LIMITS).map(([axis, max]) => `<label>${axis.toUpperCase()} / m<input type="number" data-axis="${axis}" min="${-max}" max="${max}" step="0.1"></label>`).join('')}
          </div>
          <div class="ls-shields">${['A', 'B'].map(id => `<label>${id} 初始护盾<input data-shield="${id}" type="number" min="0" step="1"></label>`).join('')}</div>
        </section>
      </div>`;
    const pick = selector => root.querySelector(selector);
    const canvas = pick('.ls-map'), play = pick('.ls-play');
    ui = {root, canvas, ctx: canvas.getContext('2d'), play, playText: play.querySelector('span'), step: pick('.ls-step'),
      rate: pick('.ls-rate select'), clock: pick('.ls-time'), axis: pick('.ls-axis'), slider: pick('.ls-scrub'),
      modes: [...root.querySelectorAll('button[data-view]')], paths: [...root.querySelectorAll('[data-path]')],
      bands: pick('.ls-bands'), pins: pick('.ls-pins'), key: pick('.ls-key'), jump: pick('.ls-jump'),
      source: pick('[data-live=source]'), subject: pick('.ls-subject'),
      coords: [...root.querySelectorAll('[data-axis]')], shields: [...root.querySelectorAll('[data-shield]')],
      vitals: Object.fromEntries([...root.querySelectorAll('[data-actor]')].map(el => [el.dataset.actor, {
        hp: el.querySelector('[data-live=hp]'), bar: el.querySelector('i'), shield: el.querySelector('[data-live=shield]'),
        burn: el.querySelector('[data-live=burn]')
      }]))};
    const style = getComputedStyle(root);
    ui.color = Object.fromEntries(['field', 'line', 'ink', 'muted', 'green', 'amber', 'blue', 'violet', 'mono'].map(key =>
      [key, style.getPropertyValue(`--${key}`).trim()]));
    new ResizeObserver(() => paintScene(true)).observe(canvas);
    bind();
  }

  function frameAt(run, time) {
    // Hold the recorded sample until the next frame, including at event boundaries.
    const frames = run?.frames || [];
    let lo = 0, hi = frames.length;
    while (lo < hi) {
      const mid = (lo + hi) >>> 1;
      if (frames[mid].time <= time + 1e-7) lo = mid + 1;
      else hi = mid;
    }
    return frames[Math.max(0, lo - 1)];
  }

  function draftShapes(setup) {
    if (lastDraft?.key === draftKey && lastDraft.setup === setup) return lastDraft.shapes;
    const shapes = new Map();
    // A contact-triggered blast may have several centers or none. Resolve them with the engine.
    for (const {fx} of window.LabSim.run(api.doc, setup, api.shared).frames) {
      for (const shape of fx) {
        const key = shape.kind === 'blast' ? `${shape.nodeId}/${shape.x}/${shape.y}` : shape.nodeId ?? shape.kind;
        if (!shapes.has(key)) shapes.set(key, shape);
      }
    }
    lastDraft = {key: draftKey, setup, shapes: [...shapes.values()]};
    return lastDraft.shapes;
  }

  function pathBetween(ctx, points) {
    ctx.beginPath();
    points.forEach(({x, y}, i) => ctx[i ? 'lineTo' : 'moveTo'](xOnMap(x), yOnMap(y)));
    ctx.stroke();
  }

  function drawGrid(ctx, color) {
    ctx.fillStyle = color.field;
    ctx.fillRect(0, 0, WIDTH, HEIGHT);
    ctx.lineWidth = 1;
    ctx.strokeStyle = color.line;
    for (let x = -6; x <= 6; x++) {
      ctx.globalAlpha = x ? .45 : .95;
      pathBetween(ctx, [{x, y: -4}, {x, y: 4}]);
    }
    for (let y = -4; y <= 4; y++) {
      ctx.globalAlpha = y ? .45 : .95;
      pathBetween(ctx, [{x: -6, y}, {x: 6, y}]);
    }
    ctx.globalAlpha = 1;
    ctx.strokeRect(xOnMap(-6), yOnMap(4), 12 * METER, 8 * METER);
    ctx.font = `9px ${color.mono}`;
    ctx.fillStyle = color.muted;
    ctx.textAlign = 'center';
    for (const x of [-6, -3, 0, 3, 6]) ctx.fillText(String(x), xOnMap(x), HEIGHT - 9);
    ctx.fillText('Y', 12, 14);
    for (const y of [-4, 4]) ctx.fillText(String(y), 10, yOnMap(y) + 3);
    ctx.fillText('X', WIDTH - 10, HEIGHT - 9);
  }

  function ring(ctx, radius, fill = false) {
    ctx.beginPath();
    ctx.arc(0, 0, radius, 0, TAU);
    if (fill) ctx.fill();
    ctx.stroke();
  }

  function drawFx(ctx, color, fx, isDraft) {
    const {kind, x, y, radius = 0, range = 0, angle = 0, direction = 0} = fx;
    ctx.save();
    ctx.translate(xOnMap(x), yOnMap(y));
    ctx.strokeStyle = ctx.fillStyle = {bullet: color.blue, melee: color.green, blast: color.amber, field: color.amber}[kind];
    const guide = isDraft || fx.phase === 'telegraph';
    ctx.lineWidth = isDraft && fx.nodeId !== api.selected ? 1.2 : 1.65;
    if (guide) ctx.setLineDash([4, 3]);
    ctx.beginPath();
    if (kind === 'melee' && range > 0) {
      const aperture = bound(angle * Math.PI / 180, 0, TAU);
      if (aperture < TAU) ctx.moveTo(0, 0);
      ctx.arc(0, 0, range * METER, -direction - aperture / 2, -direction + aperture / 2);
      ctx.closePath();
    } else if (kind === 'bullet') {
      ctx.rotate(-direction);
      if (isDraft && range > 0) {
        const length = range * METER, radiusOnMap = radius * METER;
        ctx.arc(0, 0, radiusOnMap, Math.PI / 2, Math.PI * 1.5);
        ctx.arc(length, 0, radiusOnMap, -Math.PI / 2, Math.PI / 2);
        ctx.closePath();
      } else {
        ctx.arc(0, 0, (radius || .12) * METER, 0, TAU);
      }
    } else if (radius > 0) {
      ctx.arc(0, 0, radius * METER, 0, TAU);
    }
    ctx.globalAlpha = guide ? .11 : kind === 'bullet' ? .78 : .19;
    ctx.fill();
    ctx.globalAlpha = isDraft ? .8 : 1;
    ctx.stroke();
    ctx.restore();
  }

  function drawAim(ctx, color, aim, isDraft) {
    ctx.save();
    ctx.strokeStyle = color.amber;
    ctx.globalAlpha = isDraft ? .85 : .42;
    ctx.setLineDash([4, 4]);
    pathBetween(ctx, [CASTER, aim]);
    ctx.setLineDash([]);
    ctx.translate(xOnMap(aim.x), yOnMap(aim.y));
    ring(ctx, 6.5);
    ctx.strokeRect(-9, -.35, 18, .7);
    ctx.strokeRect(-.35, -9, .7, 18);
    ctx.restore();
  }

  function drawActor(ctx, color, actor, selected) {
    const {id, x, y, hp, shield, burn} = actor;
    const tint = id === 'A' ? color.blue : id === 'B' ? color.violet : color.green;
    ctx.save();
    ctx.translate(xOnMap(x), yOnMap(y));
    ctx.globalAlpha = hp <= 0 ? .42 : 1;
    ctx.lineWidth = selected ? 2 : 1.35;
    ctx.strokeStyle = tint;
    ctx.fillStyle = color.field;
    ring(ctx, 10, true);
    if (shield > 0) { ctx.setLineDash([11, 4]); ring(ctx, 13); ctx.setLineDash([]); }
    if (burn > 0) { ctx.strokeStyle = color.amber; ring(ctx, 15); }
    ctx.fillStyle = tint;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.font = `600 10px ${color.mono}`;
    ctx.fillText(id, 0, .5);
    if (id === 'C') {
      ctx.font = '10px system-ui, sans-serif';
      ctx.textBaseline = 'top';
      ctx.fillText('施法者', 0, 15);
    }
    ctx.restore();
  }

  function paintScene(force = false) {
    if (!api || !ui) return;
    const isDraft = mode === 'draft', time = scrub?.time ?? api.time;
    const setup = isDraft ? drag?.next || api.setup : api.run?.setup;
    if (!setup) return;
    const frame = isDraft ? null : frameAt(api.run, time);
    if (!force && frame && frame === lastScene) return;
    lastScene = frame;
    const {canvas, ctx, color} = ui;
    const scale = devicePixelRatio * canvas.clientWidth / WIDTH || 1;
    const width = Math.round(WIDTH * scale), height = Math.round(HEIGHT * scale);
    if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; }
    ctx.setTransform(width / WIDTH, 0, 0, height / HEIGHT, 0, 0);
    drawGrid(ctx, color);
    ctx.save();
    ctx.beginPath();
    ctx.rect(xOnMap(-6), yOnMap(4), 12 * METER, 8 * METER);
    ctx.clip();
    if (isDraft && setup.path === 'cross') {
      ctx.strokeStyle = color.violet; ctx.setLineDash([2, 4]);
      const target = setup.targets.find(actor => actor.id === 'B');
      if (target) pathBetween(ctx, Array.from({length: 41}, (_, i) => window.LabSim.position(setup, target, i / 10)));
      ctx.setLineDash([]);
    }
    for (const fx of isDraft ? draftShapes(setup) : frame?.fx || []) drawFx(ctx, color, fx, isDraft);
    drawAim(ctx, color, setup.aim, isDraft);
    ctx.restore();
    drawActor(ctx, color, {...CASTER, id: 'C'}, false);
    const actors = isDraft ? setup.targets : frame?.actors || [];
    for (const actor of actors) drawActor(ctx, color, actor, isDraft && actor.id === subject);
    for (const [id, card] of Object.entries(ui.vitals)) {
      const actor = actors.find(item => item.id === id);
      const maxHp = setup.targets.find(item => item.id === id)?.hp || 1;
      card.hp.textContent = actor ? Number(actor.hp.toFixed(2)) : '—';
      card.bar.style.width = percent(actor?.hp ?? 0, maxHp);
      card.shield.textContent = `盾 ${actor?.shield ?? '—'}`;
      card.burn.textContent = actor?.burn ? `燃烧 ×${actor.burn}` : '';
    }
    ui.source.textContent = isDraft ? '草稿 · 初始几何' : frame ? `记录帧 · ${seconds(frame.time)}` : '无记录帧';
  }

  function timeline(run) {
    const bands = [], marks = [];
    const frames = run?.frames || [], events = run?.events || [];
    const contacts = new Set(events.filter(event => event.bulletX !== undefined).map(event => event.time));
    for (const [kind, tone] of [['melee', 'active'], ['bullet', 'flight'], ['field', 'field']]) {
      let start, last;
      for (const frame of frames) {
        if (frame.fx.some(fx => fx.kind === kind)) { start ??= frame.time; last = frame.time; }
        else if (start !== undefined) {
          const end = kind === 'bullet' && contacts.has(last) ? last : frame.time;
          bands.push({tone, start, end}); start = undefined;
        }
      }
      if (start !== undefined) bands.push({tone, start, end: run.duration});
    }
    const active = bands.find(band => band.tone === 'active');
    if (active?.start > 0) bands.unshift({tone: 'windup', start: 0, end: active.start});
    for (const {tone, start, end} of bands) {
      marks.push({tone, time: start, label: `${tones[tone]}开始`}, {tone, time: end, label: `${tones[tone]}结束`});
    }
    for (const {kind, time, label, target} of events) {
      const tone = eventTones[kind];
      if (tone) marks.push({tone, time, label: [label || tones[tone], target && `目标 ${target}`].filter(Boolean).join(' · ')});
    }
    marks.sort((a, b) => a.time - b.time);
    return {bands, marks};
  }

  function renderTimeline(run) {
    const {bands, marks} = timeline(run), duration = run?.duration || 0;
    ui.slider.max = duration;
    ui.slider.disabled = !duration;
    ui.bands.replaceChildren(...bands.map(({tone, start, end}) => {
      const bar = document.createElement('span');
      bar.className = 'ls-band'; bar.dataset.tone = tone;
      bar.style.setProperty('--at', percent(start, duration));
      bar.style.setProperty('--span', percent(end - start, duration));
      return bar;
    }));
    ui.key.replaceChildren(...Array.from(new Set([...bands, ...marks].map(item => item.tone)), tone => {
      const label = document.createElement('span'); label.dataset.tone = tone; label.textContent = tones[tone]; return label;
    }));
    const pins = new Map();
    for (const {tone, time, label} of marks) {
      const title = `${seconds(time)} · ${label}`;
      const pin = pins.get(time) || document.createElement('button');
      pin.type = 'button'; pin.className = 'ls-pin'; pin.dataset.tone = tone; pin.dataset.time = time;
      pin.style.setProperty('--at', percent(time, duration));
      pin.title = pin.title ? `${pin.title}\n${title}` : title;
      pin.setAttribute('aria-label', pin.title);
      pins.set(time, pin);
    }
    ui.pins.replaceChildren(...pins.values());
    ui.jump.replaceChildren(new Option('定位事件…', ''), ...marks.map(({time, label}) => new Option(`${seconds(time)} · ${label}`, time)));
    ui.jump.hidden = !marks.length;
  }

  const point = (setup, id) => id === 'aim' ? setup.aim : setup.targets.find(actor => actor.id === id);
  function movePoint(setup, id, next) {
    if (id === 'aim') return {...setup, aim: next};
    return {...setup, targets: setup.targets.map(actor => actor.id === id ? {...actor, ...next} : actor)};
  }

  function mapPointer(event) {
    const rect = ui.canvas.getBoundingClientRect();
    return {x: ((event.clientX - rect.left - 1) / (rect.width - 2) * WIDTH - WIDTH / 2) / METER,
      y: (HEIGHT / 2 - (event.clientY - rect.top - 1) / (rect.height - 2) * HEIGHT) / METER};
  }

  function hitHandle({x, y}) {
    let handle = null, closest = 13 / METER;
    for (const id of [subject, 'A', 'B', 'aim']) {
      const pos = point(api.setup, id), distance = pos ? Math.hypot(pos.x - x, pos.y - y) : Infinity;
      if (distance < closest) { closest = distance; handle = id; }
    }
    return handle;
  }

  function syncSetup(setup) {
    const current = point(setup, subject);
    ui.subject.value = subject;
    for (const input of ui.coords) input.value = current?.[input.dataset.axis] ?? '';
    for (const input of ui.shields) input.value = point(setup, input.dataset.shield)?.shield ?? '';
    for (const button of ui.paths) button.setAttribute('aria-pressed', String(button.dataset.path === setup.path));
  }

  function show(next) {
    if (ui.root.dataset.view === next) return;
    mode = next;
    ui.root.dataset.view = mode;
    for (const button of ui.modes) button.setAttribute('aria-pressed', String(button.dataset.view === mode));
  }

  const chooseTime = time => { show('record'); api.seek(time); };
  const changeSetup = setup => { show('draft'); api.setSetup(setup); };

  function bindMap() {
    const {canvas} = ui;
    function preview(event) {
      if (drag?.id !== event.pointerId) return;
      const now = mapPointer(event), {offset, base, handle} = drag;
      if (!drag.next && Math.hypot(now.x - drag.start.x, now.y - drag.start.y) < .04) return;
      const next = {x: snap(now.x + offset.x, 'x'), y: snap(now.y + offset.y, 'y')};
      const current = point(drag.next || base, handle);
      if (current.x === next.x && current.y === next.y) return;
      drag.next = movePoint(base, handle, next);
      syncSetup(drag.next); paintScene();
    }
    canvas.addEventListener('pointerdown', event => {
      if (mode !== 'draft' || event.button !== 0 || drag) return;
      const start = mapPointer(event), hit = hitHandle(start);
      subject = hit || 'aim';
      const base = api.setup, initial = hit ? point(base, hit) : start;
      drag = {id: event.pointerId, base, start, handle: subject, offset: {x: initial.x - start.x, y: initial.y - start.y}};
      if (!hit) drag.next = movePoint(base, 'aim', {x: snap(start.x, 'x'), y: snap(start.y, 'y')});
      canvas.setPointerCapture(event.pointerId); canvas.dataset.dragging = 'true';
      syncSetup(drag.next || base); paintScene();
    });
    canvas.addEventListener('pointermove', event => {
      if (mode !== 'draft') return;
      if (drag) preview(event);
      else canvas.dataset.grab = String(!!hitHandle(mapPointer(event)));
    });
    function finish(event) {
      if (drag?.id !== event.pointerId) return;
      const commit = event.type === 'pointerup';
      if (commit) preview(event);
      const {base, handle, next} = drag;
      drag = null;
      delete canvas.dataset.dragging;
      if (commit && next && ['x', 'y'].some(axis => point(next, handle)[axis] !== point(base, handle)[axis])) changeSetup(next);
      else { syncSetup(api.setup); paintScene(); }
    }
    for (const event of ['pointerup', 'pointercancel', 'lostpointercapture']) canvas.addEventListener(event, finish);
  }

  const snap = (value, axis) => bound(Math.round(value * 10) / 10, -LIMITS[axis], LIMITS[axis]);
  function bindTimeline() {
    ui.play.onclick = () => {
      const cast = ui.play.dataset.action === 'cast';
      show('record');
      if (cast) api.runNow();
      else api.togglePlay();
    };
    ui.step.onclick = () => { show('record'); api.step(); };
    ui.rate.onchange = () => api.setSpeed(Number(ui.rate.value));
    ui.pins.onclick = event => {
      const pin = event.target.closest('button[data-time]');
      if (pin) chooseTime(Number(pin.dataset.time));
    };
    ui.jump.onchange = () => { if (ui.jump.value !== '') chooseTime(Number(ui.jump.value)); ui.jump.value = ''; };
    const {slider} = ui;
    function preview(event) {
      const rect = slider.getBoundingClientRect();
      const fraction = bound((event.clientX - rect.left - 6) / (rect.width - 12), 0, 1);
      scrub.time = fraction * api.run.duration;
      updateClock();
    }
    slider.addEventListener('pointerdown', event => {
      if (event.button !== 0 || slider.disabled || scrub) return;
      event.preventDefault(); slider.focus({preventScroll: true}); slider.setPointerCapture(event.pointerId);
      scrub = {id: event.pointerId, previous: mode}; show('record'); preview(event);
    });
    slider.addEventListener('pointermove', event => { if (scrub?.id === event.pointerId) preview(event); });
    function finish(event) {
      if (scrub?.id !== event.pointerId) return;
      if (event.type === 'pointerup') {
        preview(event); const {time} = scrub; scrub = null; chooseTime(time);
      } else { const {previous} = scrub; scrub = null; show(previous); updateClock(); }
    }
    for (const event of ['pointerup', 'pointercancel', 'lostpointercapture']) slider.addEventListener(event, finish);
    slider.oninput = () => { if (!scrub) chooseTime(Number(slider.value)); };
  }

  function bind() {
    bindMap(); bindTimeline();
    const switchView = next => {
      if (next === 'draft' && api.playing) api.seek(api.time);
      show(next); paintScene(true); updateClock();
    };
    for (const button of ui.modes) button.onclick = () => switchView(button.dataset.view);
    ui.subject.onchange = () => { subject = ui.subject.value; syncSetup(api.setup); switchView('draft'); };
    for (const button of ui.paths) button.onclick = () => {
      if (button.dataset.path !== api.setup.path) changeSetup({...api.setup, path: button.dataset.path});
    };
    for (const input of ui.coords) input.onchange = () => {
      const original = point(api.setup, subject), axis = input.dataset.axis;
      if (!original || !Number.isFinite(input.valueAsNumber)) { syncSetup(api.setup); return; }
      const value = snap(input.valueAsNumber, axis);
      if (value !== original[axis]) changeSetup(movePoint(api.setup, subject, {...original, [axis]: value}));
      else syncSetup(api.setup);
    };
    for (const input of ui.shields) input.onchange = () => {
      const id = input.dataset.shield, shield = Math.max(0, Math.round(input.valueAsNumber));
      if (Number.isFinite(shield) && shield !== point(api.setup, id)?.shield) {
        changeSetup({...api.setup, targets: api.setup.targets.map(actor => actor.id === id ? {...actor, shield} : actor)});
      } else syncSetup(api.setup);
    };
  }

  function updateClock() {
    const duration = api.run?.duration || 0, time = scrub?.time ?? api.time;
    const ended = time >= duration - .0001;
    const needsTrial = !api.run || (mode === 'draft' && api.dirty);
    const action = api.playing ? 'pause' : needsTrial ? 'cast' : ended ? 'replay' : 'play';
    if (ui.rate.value !== String(api.speed)) ui.rate.value = api.speed;
    if (ui.play.dataset.action !== action) ui.playText.textContent = {pause: '暂停', cast: '试放', replay: '重放', play: '播放'}[action];
    ui.play.dataset.action = action;
    ui.step.disabled = !duration || ended;
    ui.clock.textContent = `${time.toFixed(2)} / ${seconds(duration)}`;
    ui.slider.value = time;
    ui.slider.setAttribute('aria-valuetext', seconds(time));
    ui.axis.style.setProperty('--progress', percent(time, duration));
    if (mode === 'record') paintScene();
  }

  function updateTime(next) {
    if (!ui || next.run !== api?.run) { render(next); return; }
    if (!scrub && (next.playing || next.time !== api.time)) show('record');
    api = next;
    updateClock();
  }

  function render(next) {
    const root = document.getElementById('stage');
    if (!root) return;
    if (ui?.root !== root) { mount(root); api = null; lastScene = null; }
    const key = JSON.stringify([next.doc, next.setup, next.shared]);
    const newRun = !api || next.run !== api.run;
    if (newRun || key !== draftKey) { scrub = drag = null; delete ui.canvas.dataset.dragging; }
    if (next.playing || (next.selectedEvent && next.selectedEvent !== api?.selectedEvent)) show('record');
    else if (!next.run || (next.dirty && key !== draftKey)) show('draft');
    else if (newRun) show('record');
    api = next; draftKey = key;
    ui.modes[1].disabled = !next.run;
    if (newRun) renderTimeline(next.run);
    syncSetup(drag?.next || next.setup); paintScene(true); updateClock();
  }

  window.LabStage = {render, updateTime};
})();
