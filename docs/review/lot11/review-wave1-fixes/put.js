// Helper for the wave 1 review fixes evidence. Reads the coach token from tokens.json (gitignored),
// never prints it. Usage:
//   node put.js <programId> <variant> [name]
// variants: base | nullBlocks | nullSessions | nullCircuits | nullExercises | custom:<file.js>
// Prints status, and for non-200 the error message and the error keys only.
const fs = require('fs');
const path = require('path');
const BASE = 'http://localhost:5243/api/v1/coach';
const token = fs.readFileSync(path.join(__dirname, 'tokens.json'), 'utf8').trim();
const H = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };

const toExercise = e => ({
  id: e.programExerciseId, exerciseId: e.exerciseId, position: e.position, loadType: e.loadType,
  prescribedSets: e.prescribedSets, prescribedReps: e.prescribedReps, prescribedWeightKg: e.prescribedWeightKg,
  restSeconds: e.restSeconds, workSeconds: e.workSeconds, restWorkSeconds: e.restWorkSeconds, customNote: e.customNote,
});
const toCircuit = c => ({
  id: c.programCircuitId, name: c.name, position: c.position, mode: c.mode, rounds: c.rounds,
  restBetweenRoundsSeconds: c.restBetweenRoundsSeconds, restAfterSeconds: c.restAfterSeconds, note: c.note,
  exercises: (c.exercises || []).map(toExercise),
});
const toSession = s => ({
  id: s.programSessionId, name: s.name, type: s.type, nature: s.nature, dayOfWeek: s.dayOfWeek, position: s.position,
  circuits: (s.circuits || []).map(toCircuit),
});
const toBlock = b => ({
  id: b.programBlockId, level: b.level, name: b.name, position: b.position, weekNumber: b.weekNumber,
  loadPercent: b.loadPercent, blocks: (b.blocks || []).map(toBlock), sessions: (b.sessions || []).map(toSession),
});

async function api(method, url, body) {
  const r = await fetch(BASE + url, { method, headers: H, body: body === undefined ? undefined : JSON.stringify(body) });
  let j = null; try { j = await r.json(); } catch { /* empty body */ }
  return { status: r.status, json: j };
}

async function getProgram(id) {
  const r = await api('GET', `/training-programs/${id}`);
  return r.json.data.program ?? r.json.data;
}

// First node of each kind in the request tree (depth first).
function firstOf(blocks, kind) {
  for (const b of blocks) {
    if (kind === 'block') return b;
    const inner = firstOf(b.blocks || [], kind);
    if (inner) return inner;
    for (const s of b.sessions || []) {
      if (kind === 'session') return b;
      for (const c of s.circuits || []) {
        if (kind === 'circuit') return s;
        if (kind === 'exercise') return c;
      }
    }
  }
  return null;
}

function buildRequest(p, name) {
  return {
    name: name ?? p.name, goal: p.goal, startDate: p.startDate, durationWeeks: p.durationWeeks,
    body: { bodyVersion: 1, blocks: p.blocks.map(toBlock) },
  };
}

function applyVariant(req, variant) {
  const b = req.body;
  if (variant === 'nullBlocks') b.blocks = [null];
  else if (variant === 'nullSessions') firstOf(b.blocks, 'session').sessions = [null];
  else if (variant === 'nullCircuits') firstOf(b.blocks, 'circuit').circuits = [null];
  else if (variant === 'nullExercises') firstOf(b.blocks, 'exercise').exercises = [null];
  return req;
}

function summarize(r) {
  const out = { status: r.status };
  const j = r.json;
  if (j && r.status !== 200 && r.status !== 201) {
    out.error = j.error;
    if (j.errors) out.errorKeys = Object.keys(j.errors);
    if (j.details) out.details = j.details;
  }
  return out;
}

module.exports = { api, getProgram, buildRequest, applyVariant, summarize, firstOf, toBlock, BASE };

if (require.main === module) {
  (async () => {
    const [id, variant = 'base', name] = process.argv.slice(2);
    const p = await getProgram(id);
    const req = applyVariant(buildRequest(p, name), variant);
    const r = await api('PUT', `/training-programs/${id}`, req);
    console.log(variant, JSON.stringify(summarize(r)));
  })();
}
