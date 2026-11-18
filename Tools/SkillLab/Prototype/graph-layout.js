(() => {
  'use strict';

  const WIDTH = 176, FACE = 56, PORT = 27, STACKED_PORT = 39;
  const COLUMN_GAP = 42, ROW_GAP = 38;
  const isInput = node => node.id === 'cast' || node.kind === 'cast';
  const isEventPort = port => ['target', 'contact', 'event'].includes(port.context);

  function indexGraph(doc, catalog) {
    const boxes = new Map(doc.nodes.map(node => {
      const ports = catalog[node.kind]?.ports ?? [];
      return [node.id, {
        node, ports, out:[], incoming:0, width:WIDTH,
        height:FACE + (ports.length >= 3 ? STACKED_PORT : ports.length ? PORT : 0)
      }];
    }));
    for (const {from, port, to} of doc.edges) {
      const source = boxes.get(from), destination = boxes.get(to);
      const index = source?.ports.findIndex(output => output.key === port) ?? -1;
      if (!destination || index < 0) continue;
      source.out.push({port:index, to:destination});
      destination.incoming++;
    }
    for (const box of boxes.values()) box.out.sort((a, b) => a.port - b.port);
    return boxes;
  }

  function layers(boxes, roots) {
    const pending = new Set(boxes.values()), rows = [];
    let frontier = roots;
    while (pending.size) {
      const row = frontier.filter(box => pending.delete(box));
      if (!row.length) { frontier = [[...pending][0]]; continue; }
      rows.push(row);
      // The order of the drawn ports controls the order of sibling lanes.
      frontier = row.flatMap(box => box.out.map(edge => edge.to));
    }
    return rows;
  }

  function compactRows(ranks) {
    const rows = [];
    for (let i = 0; i < ranks.length; i++) {
      const row = ranks[i], [source] = row, next = ranks[i + 1];
      const destination = next?.[0], edge = source.out[0];
      // Pair simple chain steps in one row. Fan-out keeps its own full row.
      if (row.length === 1 && next?.length === 1 && source.ports.length === 1
        && source.out.length === 1 && edge.to === destination && destination.incoming === 1) {
        edge.side = destination.sideInput = true;
        rows.push([source, destination]);
        i++;
      } else rows.push(row);
    }
    return rows;
  }

  function curve(x, y, endX, endY, horizontal) {
    const middle = horizontal ? (x + endX) / 2 : (y + endY) / 2;
    return horizontal
      ? `M${x} ${y} C${middle} ${y} ${middle} ${endY} ${endX} ${endY}`
      : `M${x} ${y} C${x} ${middle} ${endX} ${middle} ${endX} ${endY}`;
  }

  function wire(from, {port, to, side}) {
    const path = side
      ? curve(from.x + WIDTH + 3, from.y + FACE + PORT / 2, to.x - 4, to.y + FACE / 2, true)
      : curve(from.x + WIDTH * (port + .5) / from.ports.length, from.y + from.height + 3,
        to.x + WIDTH / 2, to.y - 4, false);
    return {path, kind:isEventPort(from.ports[port]) ? 'event' : 'flow'};
  }

  function layout(doc, catalog) {
    const boxes = indexGraph(doc, catalog);
    const roots = [...boxes.values()].filter(box => !box.incoming || isInput(box.node));
    const rows = compactRows(layers(boxes, roots));
    const rowWidth = row => row.length * (WIDTH + COLUMN_GAP) - COLUMN_GAP;
    const width = Math.max(WIDTH, ...rows.map(rowWidth));
    let bottom = 0;
    for (const row of rows) {
      const left = (width - rowWidth(row)) / 2;
      row.forEach((box, i) => {
        box.x = left + i * (WIDTH + COLUMN_GAP);
        box.y = bottom;
      });
      bottom += Math.max(...row.map(box => box.height)) + ROW_GAP;
    }
    const cards = rows.flat(), wires = cards.flatMap(box => box.out.map(edge => wire(box, edge)));
    const root = boxes.size === 1 && roots.find(box => isInput(box.node) && box.ports.length);
    const starter = root ? {node:root.node, port:root.ports[0], x:root.x, y:bottom, width:WIDTH} : null;
    if (starter) {
      const x = root.x + WIDTH / (root.ports.length * 2);
      wires.push({path:curve(x, root.y + root.height + 5, starter.x + WIDTH / 2, starter.y - 4, false), kind:'starter'});
    }
    return {boxes, roots, cards, wires, starter, width, face:FACE,
      height:starter ? starter.y + 40 : Math.max(64, bottom - ROW_GAP)};
  }

  window.LabGraphLayout = {layout, isEventPort, isInput};
})();
