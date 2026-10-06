// Tree helper for the 11.7 runtime proofs, copied from docs/review/lot11/review-wave1-fixes/put.js with the
// two renamed rest fields. Reads the coach token from tokens.json (gitignored), never prints it.
const fs = require('fs');
const path = require('path');
const BASE = 'http://localhost:5243/api/v1/coach';
const token = fs.readFileSync(path.join(__dirname, 'tokens.json'), 'utf8').trim();
const H = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' };

const toExercise = e => ({
  id: e.programExerciseId, exerciseId: e.exerciseId, position: e.position, loadType: e.loadType,
  prescribedSets: e.prescribedSets, prescribedReps: e.prescribedReps, prescribedWeightKg: e.prescribedWeightKg,
  restSecondsAfterSeries: e.restSecondsAfterSeries, workSeconds: e.workSeconds, restWorkSeconds: e.restWorkSeconds,
  customNote: e.customNote,
});
const toCircuit = c => ({
  id: c.programCircuitId, name: c.name, position: c.position, mode: c.mode, rounds: c.rounds,
  restBetweenRoundsSeconds: c.restBetweenRoundsSeconds, restSecondsAfterExercices: c.restSecondsAfterExercices,
  note: c.note, exercises: (c.exercises || []).map(toExercise),
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
  const text = await r.text();
  let j = null; try { j = JSON.parse(text); } catch { /* empty body */ }
  return { status: r.status, json: j, text };
}

function programOf(json) { return json.data.program ?? json.data; }

function buildRequest(p, name) {
  return {
    name: name ?? p.name, goal: p.goal, startDate: p.startDate, durationWeeks: p.durationWeeks,
    body: { bodyVersion: 1, blocks: p.blocks.map(toBlock) },
  };
}

// Every circuit / exercise object of a request tree, depth first.
function circuitsOf(blocks, out = []) {
  for (const b of blocks) { circuitsOf(b.blocks || [], out); for (const s of b.sessions || []) for (const c of s.circuits || []) out.push(c); }
  return out;
}

module.exports = { api, programOf, buildRequest, circuitsOf, BASE };
