// Flowcharts retain the original literal structure comparison. For sequences and
// state charts keep participants, arrow kinds, control blocks and source order.
export function diagramModel(source) {
  const lines = source.split(/\r?\n/).filter(s => !s.trim().startsWith('%%') || s.trim().startsWith('%% event '));
  const kind = lines.find(s => s.trim())?.trim();
  if (!['sequenceDiagram', 'stateDiagram-v2'].includes(kind)) {
    return { kind: 'flowchart', topology: lines.join('\n').replace(/"[^"]*"/g, '"LABEL"') };
  }
  const topology = [], labels = [];
  const eventIds = new Set();
  let event;
  for (const raw of lines) {
    const line = raw.trim();
    if (!line) continue;
    if (line.startsWith('%% event ')) {
      const id = line.slice(9);
      if (event || eventIds.has(id) || !/^[a-z][a-z0-9-]+$/.test(id)) throw new Error('Invalid diagram event: ' + line);
      eventIds.add(id);
      event = id;
      continue;
    }
    const part = kind === 'sequenceDiagram'
      ? line.match(/^(actor \w+ as|participant \w+ as|\w+[-.]+>>?\w+:|alt|else|loop|opt|break|par|and) (.+)$/)
      : line.match(/^(\S+ --> \S+:) (.+)$/);
    if (part) {
      const message = part[1].endsWith(':');
      if (!!event !== message) throw new Error('Missing or misplaced diagram event: ' + line);
      topology.push(event ? event + '|' + part[1] : part[1]);
      event = undefined;
      labels.push(part[2]);
    }
    else if (line === kind || line === 'end' || (kind === 'stateDiagram-v2' && /^\S+ --> \S+$/.test(line))) topology.push(line);
    else throw new Error('Unrecognized diagram statement: ' + line);
  }
  if (event) throw new Error('Diagram event without a message: ' + event);
  return {kind, topology, labels};
}
