// Immutable trial records support two views: an event at one time, and the final outcome.
(() => {
    'use strict';
    const byId = id => document.getElementById(id);
    const make = (tag, text) => Object.assign(document.createElement(tag), { textContent: text });
    const option = (value, label) => new Option(label, value);
    const eventNames = {hit: '命中'};
    function eventName(event) { return eventNames[event.kind] || event.label || event.kind; }
    function summarize(trial) {
        const values = new Map();
        for (const actor of trial.frames.at(-1)?.actors || []) {
            for (const [key, label] of [['hp', '生命'], ['shield', '护盾'], ['burn', '燃烧层数']]) {
                values.set(`${actor.id} · ${label}`, actor[key] ?? 0);
            }
        }
        for (const event of trial.events) {
            const key = `${event.target || '场景'} · ${event.label || event.kind}次数`;
            values.set(key, (values.get(key) || 0) + 1);
        }
        return [...values];
    }
    function comparison(api) {
        const area = byId('comparison');
        area.replaceChildren();
        if (!api.baseline)
            return;
        const base = new Map(api.baseline.values), actual = new Map(summarize(api.run));
        const deltas = [];
        for (const key of new Set([...base.keys(), ...actual.keys()])) {
            const from = base.get(key) ?? 0, to = actual.get(key) ?? 0;
            if (from !== to)
                deltas.push(`${key} ${from} → ${to}`);
        }
        area.append(`预期 #${api.baseline.id} / 当前记录 #${api.run.id}　`);
        if (!deltas.length) {
            area.append('目标末态和事件次数一致');
            return;
        }
        const details = document.createElement('details');
        details.append(make('summary', `${deltas.length} 项变化 · ${deltas[0]}`));
        for (const change of deltas)
            details.append(make('div', change));
        area.append(details);
    }
    function outcomes(run) {
        const host = byId('outcomes');
        host.replaceChildren();
        for (const a of run.frames.at(-1)?.actors || []) {
            const line = make('div', '');
            line.className = 'outcome';
            const before = run.setup.targets.find(t => t.id === a.id);
            line.append(make('strong', `目标 ${a.id}`), `生命 ${before?.hp ?? '–'} → ${a.hp}`);
            const flags = make('span', `护盾 ${a.shield}　燃烧 ${a.burn || 0} 层`);
            flags.className = 'flag';
            line.append(document.createElement('br'), flags);
            host.append(line);
        }
    }
    function filtered(api) {
        const target = byId('filter-target').value, type = byId('filter-event').value;
        return api.run.events.map((event, i) => ({ event, i })).filter(({ event }) => (target === '*' || target === event.target) && (type === '*' || type === event.kind));
    }
    function rows(api) {
        const log = byId('trace-log'), scrollTop = log.scrollTop;
        log.replaceChildren();
        for (const { event, i } of filtered(api)) {
            const row = make('button', '');
            row.type = 'button';
            row.className = 'trace-row';
            row.dataset.eventIndex = String(i);
            row.setAttribute('aria-pressed', String(api.selectedEvent === event));
            row.append(make('time', `${event.time.toFixed(2)} s`), make('span', event.target || '场景'), make('strong', event.label), make('small', event.detail || ''));
            log.append(row);
        }
        if (!log.children.length)
            log.append(make('small', '此记录没有符合筛选条件的事件。'));
        log.scrollTop = scrollTop;
    }
    function render(api) {
        const records = byId('record'), activeKind = byId('filter-event').value;
        records.replaceChildren(...api.history.map(r => option(r.id, `#${r.id} · ${r.events.length} 个事件`)));
        records.value = String(api.run.id);
        const kinds = new Map(api.run.events.map(e => [e.kind, eventName(e)]));
        byId('filter-event').replaceChildren(option('*', '全部'), ...[...kinds].map(([id, label]) => option(id, label)));
        byId('filter-event').value = kinds.has(activeKind) ? activeKind : '*';
        outcomes(api.run);
        comparison(api);
        rows(api);
    }
    window.LabRecords = { render, rows, summarize };
})();
