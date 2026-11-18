(() => {
    'use strict';
    const M = window.LabModel, Sim = window.LabSim, A = window.LabAuthor, S = window.LabStage, R = window.LabRecords;
    const $ = id => document.getElementById(id), clone = value => JSON.parse(JSON.stringify(value));
    const storageKey = 'halocombat.skill-workbench.prototype.1';
    const ids = M.templates.map(t => t.id), docs = {}, setups = {}, histories = {}, expected = {};
    let shared = M.sharedDefaults(), active = 'blast', selected = 'cast', chosenRun = null, selectedEvent = null;
    let time = 0, playing = false, speed = 1, tick = 0, lastTick = 0, runSequence = 0, frame = 0, toastTimer;
    const undo = [], redo = [];
    const labels = { melee: ['近战横扫', '动作窗口', '#e3b082'], bullet: ['直线子弹', '接触与穿透', '#95cde4'], blast: ['爆裂火球', '范围与扣血', '#ecae75'], field: ['持续火圈', '周期与出入', '#a0d8ac'], blank: ['空白技能', '从施放开始', '#b6adc4'] };
    const hints = { melee: '调整前摇和扇形角度，观察 A、B 的首次命中时间。', bullet: '从弹体命中端口添加穿透，重新施放，看后方目标是否受伤。', blast: '调节目标 B 的初始护盾，比较实际扣血和燃烧层数。', field: '让目标 B 往返穿过区域，查看进入、周期和离开事件。', blank: '从施放端口的加号开始搭建一支新技能。' };
    const symbols = { melee: '<path d="M5 27 12 7a25 25 0 0 1 18 16Z" fill="currentColor" opacity=".2"/><path d="m5 27 7-20a25 25 0 0 1 18 16L5 27Zm0 0L23 14"/>', bullet: '<path d="m5 23 12-4M3 28l11-4M8 16l8-2"/><path d="m16 21 3-8 12-5-5 12-8 4Z" fill="currentColor" opacity=".8"/>', blast: '<circle cx="17" cy="18" r="12" stroke-dasharray="3 3"/><path d="m16 9 3 7 7 1-7 3-2 7-3-7-6-2 6-2Z" fill="currentColor"/>', field: '<ellipse cx="17" cy="22" rx="14" ry="6"/><path d="M3 18c0 8 28 8 28 0M8 13l2-4m7 5 2-8m5 9 3-5"/>', blank: '<path d="M7 4h15l5 5v20H7Zm12 0v7h8M12 20h10m-5-5v10"/>' };
    for (const id of ids) {
        docs[id] = M.create(id);
        setups[id] = M.setup(id);
        histories[id] = [];
    }
    const primaryNode = (doc) => doc.nodes.find(n => ['blast', 'melee', 'bullet', 'field'].includes(n.kind) && (n.kind === doc.templateId || doc.templateId === 'blank'))?.id || doc.nodes[0].id;
    function persist() {
        try {
            localStorage.setItem(storageKey, JSON.stringify({ active, docs, setups, shared, expected }));
            $('local-status').textContent = '草稿已存本机';
        }
        catch {
            $('local-status').textContent = '当前窗口草稿';
        }
    }
    function load() {
        try {
            const value = JSON.parse(localStorage.getItem(storageKey));
            if (!value)
                return;
            for (const id of ids) {
                const d = value.docs?.[id], s = value.setups?.[id];
                if (d?.templateId === id && Array.isArray(d.nodes) && d.nodes.some(n => n.id === 'cast') && d.nodes.every(n => M.catalog[n.kind]) && Array.isArray(d.edges) && s?.targets?.length === 2) {
                    docs[id] = d;
                    setups[id] = s;
                }
                if (Array.isArray(value.expected?.[id]?.values)) {
                    expected[id] = value.expected[id];
                    runSequence = Math.max(runSequence, expected[id].id);
                }
            }
            if (value.shared?.burn?.name)
                shared = value.shared;
            if (ids.includes(value.active))
                active = value.active;
        }
        catch { /* A fresh set of examples is always usable when local storage is unavailable. */ }
    }
    function refs(key) { return ids.filter(id => docs[id].nodes.some(n => n.kind === 'burn' && (n.params.shared || 'burn') === key)).map(id => docs[id].title); }
    function fingerprint() { const used = {}; for (const n of docs[active].nodes)
        if (n.kind === 'burn') {
            const key = n.params.shared || 'burn';
            used[key] = shared[key];
        } return JSON.stringify([docs[active], setups[active], used]); }
    let currentStamp = '';
    function api() { return { doc: docs[active], selected, setup: setups[active], shared, history: histories[active], baseline: expected[active], run: chosenRun, selectedEvent, time, playing, speed, dirty: chosenRun?.stamp !== currentStamp, select, setParam, addNode, removeNode, modifyShared, forkShared, refs, toast, setSetup, runNow, togglePlay, step, seek, setSpeed }; }
    function toast(text) { $('notification').textContent = text; clearTimeout(toastTimer); toastTimer = setTimeout(() => $('notification').textContent = '', 2700); }
    function stop() { playing = false; cancelAnimationFrame(frame); frame = 0; }
    function makeTrial() {
        const doc = clone(docs[active]), setup = clone(setups[active]), defs = clone(shared);
        const result = Sim.run(doc, setup, defs);
        const r = { ...result, id: ++runSequence, doc, setup, shared: defs, stamp: currentStamp };
        histories[active].unshift(r);
        if (histories[active].length > 7)
            histories[active].pop();
        return r;
    }
    function meta() {
        const current = api();
        $('skill-title').textContent = current.doc.title;
        $('skill-description').textContent = hints[active];
        $('run-status').textContent = current.dirty ? '草稿已改 · 待试放' : `记录 #${chosenRun.id} · 已试放`;
        $('run-status').classList.toggle('changed', current.dirty);
        $('undo').disabled = !undo.length;
        $('redo').disabled = !redo.length;
        for (const b of $('skills').children)
            b.setAttribute('aria-pressed', String(b.dataset.skill === active));
    }
    function render() { const current = api(); meta(); A.render(current); S.render(current); R.render(current); }
    function clock() { S.updateTime(api()); }
    function playLoop(now) {
        if (!playing)
            return;
        time = Math.min(chosenRun.duration, time + (now - lastTick) * speed / 1000);
        lastTick = now;
        if (time >= chosenRun.duration) {
            stop();
            clock();
            return;
        }
        if (now - tick >= 30) {
            clock();
            tick = now;
        }
        frame = requestAnimationFrame(playLoop);
    }
    function play() { lastTick = performance.now(); tick = 0; playing = true; clock(); frame = requestAnimationFrame(playLoop); }
    function runNow() { stop(); currentStamp = fingerprint(); chosenRun = makeTrial(); selectedEvent = null; time = 0; render(); play(); }
    function togglePlay() { if (playing) {
        stop();
        clock();
        return;
    } if (time >= chosenRun.duration - .0001)
        time = 0; play(); }
    function seek(t) { stop(); time = Math.min(chosenRun.duration, Math.max(0, Number(t) || 0)); clock(); }
    function step() { seek(time + .02); }
    function setSpeed(value) { speed = Math.min(4, Math.max(.1, Number(value) || 1)); clock(); }
    function select(id) { if (!docs[active].nodes.some(n => n.id === id))
        return; selected = id; A.render(api()); S.render(api()); }
    function chooseSkill(id) { if (!ids.includes(id))
        return; stop(); active = id; selected = primaryNode(docs[id]); selectedEvent = null; currentStamp = fingerprint(); chosenRun = histories[id][0] || makeTrial(); time = chosenRun.events.find(e => e.kind === 'damage')?.time ?? 0; $('filter-target').value = '*'; $('filter-event').value = '*'; render(); persist(); }
    function snapshot() { return clone({ active, doc: docs[active], setup: setups[active], shared, selected }); }
    function change(fn) { const before = snapshot(); stop(); fn(); undo.push(before); if (undo.length > 60)
        undo.shift(); redo.length = 0; currentStamp = fingerprint(); selectedEvent = null; render(); persist(); }
    function travel(source, destination) { if (!source.length)
        return; stop(); const next = source.pop(); const old = active; active = next.active; destination.push({ ...snapshot(), selected: old === active ? selected : primaryNode(docs[active]) }); docs[active] = next.doc; setups[active] = next.setup; shared = next.shared; selected = next.selected; selectedEvent = null; currentStamp = fingerprint(); chosenRun = histories[active][0] || makeTrial(); time = 0; render(); persist(); }
    function find(id) { return docs[active].nodes.find(n => n.id === id); }
    function setParam(id, key, value) { const n = find(id); if (!n || n.params[key] === value)
        return; change(() => { n.params[key] = value; }); }
    function newId(prefix) { return prefix + '-' + (globalThis.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(36).slice(2)}`); }
    function addNode(parent, port, kind) { const doc = docs[active], choice = M.choices(doc, parent, port).find(c => c.kind === kind); if (!choice?.enabled) {
        toast(choice?.reason || '此处不能添加该节点');
        return;
    } const id = newId(kind); change(() => { doc.nodes.push(M.makeNode(kind, id)); doc.edges.push({ from: parent, port, to: id }); selected = id; }); return id; }
    function removeNode(id) {
        const doc = docs[active];
        if (id === 'cast' || !find(id))
            return;
        change(() => {
            const edges = doc.edges.filter(e => e.from !== id && e.to !== id);
            const alive = new Set(['cast']);
            const pending = ['cast'];
            while (pending.length) {
                const n = pending.pop();
                for (const e of edges.filter(e => e.from === n)) {
                    if (!alive.has(e.to)) {
                        alive.add(e.to);
                        pending.push(e.to);
                    }
                }
            }
            const parent = doc.edges.find(e => e.to === id)?.from;
            doc.nodes = doc.nodes.filter(n => alive.has(n.id));
            doc.edges = edges.filter(e => alive.has(e.from) && alive.has(e.to));
            selected = alive.has(parent) ? parent : 'cast';
        });
    }
    function modifyShared(id, value) { const n = find(id); if (n?.kind !== 'burn')
        return; const key = n.params.shared || 'burn', stacks = Math.max(1, Math.min(99, Math.round(Number(value) || 1))); if (shared[key].stacks === stacks)
        return; change(() => { shared[key].stacks = stacks; }); }
    function forkShared(id) { const n = find(id); if (n?.kind !== 'burn')
        return; const source = shared[n.params.shared || 'burn']; change(() => { const key = newId('burn'); shared[key] = { ...source, name: '燃烧 · 独立副本' }; n.params.shared = key; }); }
    function setSetup(next) { if (!next || JSON.stringify(setups[active]) === JSON.stringify(next))
        return; change(() => { setups[active] = clone(next); }); }
    for (const id of ids) {
        const info = labels[id], b = document.createElement('button');
        b.type = 'button';
        b.className = 'template';
        b.dataset.skill = id;
        b.style.setProperty('--icon', info[2]);
        b.innerHTML = `<svg viewBox="0 0 34 34" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true">${symbols[id]}</svg><span><strong>${info[0]}</strong><small>${info[1]}</small></span>`;
        b.onclick = () => chooseSkill(id);
        $('skills').append(b);
        if (id !== 'blank') {
            const g = document.createElement('button');
            g.type = 'button';
            g.className = 'guide-choice';
            g.innerHTML = `<b>${info[0]}</b><small>${hints[id]}</small>`;
            g.onclick = () => { $('guide').close(); chooseSkill(id); };
            $('guide-options').append(g);
        }
    }
    $('cast').onclick = runNow;
    $('undo').onclick = () => travel(undo, redo);
    $('redo').onclick = () => travel(redo, undo);
    $('help').onclick = () => $('guide').showModal();
    $('close-guide').onclick = $('guide-done').onclick = () => $('guide').close();
    $('baseline').onclick = () => { expected[active] = { id: chosenRun.id, values: R.summarize(chosenRun) }; R.render(api()); persist(); toast(`已记录 #${chosenRun.id} 的预期结果`); };
    $('record').onchange = e => { stop(); chosenRun = histories[active].find(r => r.id === Number(e.target.value)); selectedEvent = null; time = 0; render(); };
    $('filter-target').onchange = $('filter-event').onchange = () => R.rows(api());
    $('trace-log').onclick = e => { const row = e.target.closest('[data-event-index]'); if (!row)
        return; stop(); selectedEvent = chosenRun.events[Number(row.dataset.eventIndex)]; time = selectedEvent.time; if (find(selectedEvent.nodeId))
        selected = selectedEvent.nodeId; render(); if (api().dirty)
        toast('已定位历史事件，执行明细沿用当次配置'); };
    document.addEventListener('keydown', e => { if (e.target.closest('input,select,textarea,dialog,[contenteditable=true]') || document.querySelector('dialog[open]'))
        return; if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
        e.preventDefault();
        if (e.shiftKey)
            travel(redo, undo);
        else
            travel(undo, redo);
    }
    else if (e.code === 'Space' && !e.repeat && !e.target.closest('button,summary')) {
        e.preventDefault();
        togglePlay();
    } });
    $('jump-results').onclick = () => $('trial-results').scrollIntoView({block:'start', behavior:'auto'});
    load();
    chooseSkill(active);
})();
