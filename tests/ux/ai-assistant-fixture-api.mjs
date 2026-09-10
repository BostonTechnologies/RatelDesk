// Loopback-only deterministic API fixture for the real NewWeb ticket component.
// This is browser presentation evidence, not live AiAssistant/authorization evidence.
import http from 'node:http';
const conversation = '11111111-1111-4111-8111-111111111111';
const actor = 'c0c48409df3ca07e12725a961d4152790fd135c2384a3e3a522282e2bd847178';
let events = [], state = 0;
const streams = new Set();
function event(type, text, extra = {}) {
  const value = { Id: crypto.randomUUID(), ConversationId: conversation, Sequence: events.length + 1,
    Type: type, Text: text, CreatedUtc: new Date().toISOString(), ...extra };
  events.push(value);
  for (const stream of streams) stream.write(`id: ${value.Sequence}\nevent: chat\ndata: ${JSON.stringify(value)}\n\n`);
  return value;
}
function reset(mode = 'completed') {
  events = []; state = mode === 'processing' ? 1 : mode === 'approval' ? 2 : mode === 'archived' ? 4 : 0;
  event('created', 'Conversation created.');
  if (mode === 'empty') return;
  event('operator', 'Investigate the Helpdesk integration and explain the result.', { CreatedByUserId: actor, ClientMessageId: crypto.randomUUID() });
  for (let i = 0; i < 14; i++) {
    const metadata = { DisplayName: i === 12 ? 'terminal_execute_12_with_a_long_tool_identifier_that_must_wrap_inside_the_activity_panel'
      : `terminal_execute_${i}`, ToolKind: 'mcp', ProviderName: 'Example Provider', Outcome: 'running' };
    event('tool_call', 'tool: started', { CallId: `call-${i}`, MetadataJson: JSON.stringify(metadata) });
    if (mode !== 'processing' || i < 13) event('tool_result', 'tool: finished', { CallId: `call-${i}`,
      MetadataJson: JSON.stringify({ ...metadata, Outcome: mode === 'failed' && i === 13 ? 'failed' : 'completed', DurationMs: 1851 }) });
  }
  if (mode === 'approval') event('approval_request', 'Allow this tool operation?', { CallId: 'approval-1', OptionsJson: JSON.stringify([{ Key: 'approve_once', Label: 'Allow once' }, { Key: 'deny', Label: 'Deny' }]) });
  if (!['processing', 'approval'].includes(mode)) {
    event('assistant', 'The integration is responding normally.\n\n• Ticket context reached the harness.\n• Tool activity was recorded safely.\n\nNo production changes were made.');
    event('turn_completed', 'Turn completed.');
  }
}
reset();
http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://127.0.0.1');
  const reply = value => { res.setHeader('Content-Type', 'application/json'); res.end(JSON.stringify(value)); };
  if (url.pathname === '/fixture/reset') { reset(url.searchParams.get('mode')); return reply({ ok: true }); }
  if (url.pathname === '/fixture/draft') {
    for (const stream of streams) stream.write(`event: text-delta\ndata: ${JSON.stringify({ ConversationId: conversation, Text: 'Streaming response', AfterSequence: events.length })}\n\n`);
    return reply({ ok: true });
  }
  if (url.pathname === '/fixture/complete') {
    event('assistant', 'Canonical response'); event('turn_completed', 'Turn completed.'); state = 0;
    for (const stream of streams) stream.write('event: state\ndata: 0\n\n');
    return reply({ ok: true });
  }
  if (url.pathname === '/fixture/replay') {
    for (const stream of streams) for (const item of events)
      stream.write(`id: ${item.Sequence}\nevent: chat\ndata: ${JSON.stringify(item)}\n\n`);
    return reply({ ok: true });
  }
  if (url.pathname.endsWith('/system/token')) {
    const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
    return reply({ token: `${encode({ alg: 'none' })}.${encode({ sub: actor, auth_mode: 'development', preferred_username: 'UX Test Operator', roles: ['HelpdeskAdmin', 'Incident.User'], exp: Math.floor(Date.now()/1000)+3600 })}.fixture` });
  }
  if (url.pathname === '/api/v1/branding') return reply({ applicationName: 'RatelDesk', faviconUrl: '/favicon.ico' });
  if (url.pathname.endsWith('/chat/stream')) {
    res.writeHead(200, { 'Content-Type': 'text/event-stream' });
    for (const item of events.filter(x => x.Sequence > Number(url.searchParams.get('cursor') ?? 0))) res.write(`id: ${item.Sequence}\nevent: chat\ndata: ${JSON.stringify(item)}\n\n`);
    res.write(`event: state\ndata: ${state}\n\n`);
    streams.add(res); req.on('close', () => streams.delete(res)); return;
  }
  if (url.pathname.endsWith('/chat/history')) return reply([{ conversationId: conversation, state, lastActivityUtc: new Date().toISOString(), createdByUserId: actor }]);
  if (url.pathname.endsWith('/chat/stop-waiting')) {
    state = 3; event('delivery_unknown', 'Operator stopped waiting locally.');
    for (const stream of streams) stream.write('event: state\ndata: 3\n\n');
    return reply({ accepted: true });
  }
  if (url.pathname.endsWith('/chat/messages')) {
    let body = ''; for await (const part of req) body += part;
    const message = JSON.parse(body);
    if (state !== 0) { res.statusCode = 409; return reply({ error: 'busy' }); }
    state = 1; event('operator', message.text, { ClientMessageId: message.clientMessageId, CreatedByUserId: actor });
    for (const stream of streams) stream.write('event: state\ndata: 1\n\n');
    return reply({ accepted: true });
  }
  if (url.pathname.endsWith('/chat')) return reply({ conversationId: conversation, state, lastSequence: events.length, events });
  if (url.pathname === '/api/v1/incidents/ux-chat') return reply({ id: 'ux-chat', subject: 'AiAssistant UX fixture', trackingId: 'UX-799', state: 0, priority: 1, categoryIds: [] });
  if (url.pathname.endsWith('/count')) return reply(0);
  if (url.pathname.includes('summary')) return reply({});
  if (url.pathname.includes('/stream')) { res.writeHead(200, { 'Content-Type': 'text/event-stream' }); return res.end(); }
  return reply([]);
}).listen(18299, '127.0.0.1', () => console.log('UX fixture API on loopback :18299'));
