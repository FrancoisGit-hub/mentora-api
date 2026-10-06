// Runtime proofs of the wave 1 review fixes. Usage: node proofs.js nowrite | node proofs.js write
// Prints statuses, 422 error keys and 409 details only. Never prints the token.
const { execFileSync } = require('child_process');
const { api, getProgram, buildRequest, applyVariant, firstOf } = require('./put.js');

const P = 'e5441b48-399a-409c-82f5-e3061ea730a4';
const COACH = 'e268a512-84d6-4e27-b5c0-1d6c08a47190';
const RUN_START = '2026-10-06 07:58:35.064101+00';
const X1 = '78aca3c2-f528-489f-9c1e-5203e2f5db77'; // KG, actuals
const X2 = '7d6d6bd4-b2c3-4e26-8692-a78ceaeb58a8'; // KG, actuals
const X3 = 'cc674e8c-d712-4797-8918-c15024418abe'; // KG, actuals
const X4 = '38a272c1-1e32-49db-8afc-74b44028d088'; // BODYWEIGHT, actuals, last of its circuit
const N1 = '65402458-a0e8-4c3e-b1a2-166a6841d57d'; // no actuals, session B pos 1
const N2 = '7f6b6efb-3e78-4cf2-8b3f-c6c38946e69a'; // no actuals, session B pos 2
const OTHER_EXERCISE = '72b9431c-9086-46ab-ba0e-2671f69702da'; // active, visible (seed)

function sql(text) {
  return execFileSync('docker', ['exec', '-i', 'mentora-postgres-local', 'psql', '-U', 'mentora_user', '-d', 'mentora_db', '-At', '-v', 'ON_ERROR_STOP=1'],
    { input: text, encoding: 'utf8' }).trim();
}
const rows = (t, idCol, where) =>
  `SELECT '${t}', count(*), md5(coalesce(string_agg(x::text || ':' || x.xmin::text, '|' ORDER BY x."${idCol}"), '')) FROM "${t}" x WHERE ${where}`;
function fingerprint() {
  return sql([
    rows('PROGRAMS', 'PROGRAM_ID', `x."PROGRAM_COACH_ID"='${COACH}' AND x."PROGRAM_CREATED_DATE" >= '${RUN_START}'`),
    rows('PROGRAM_BLOCKS', 'PROGRAM_BLOCK_ID', `x."PROGRAM_BLOCK_PROGRAM_ID"='${P}'`),
    rows('PROGRAM_SESSIONS', 'PROGRAM_SESSION_ID', `x."PROGRAM_SESSION_PROGRAM_ID"='${P}'`),
    rows('PROGRAM_CIRCUITS', 'PROGRAM_CIRCUIT_ID', `x."PROGRAM_CIRCUIT_PROGRAM_ID"='${P}'`),
    rows('PROGRAM_EXERCISES', 'PROGRAM_EXERCISE_ID', `x."PROGRAM_EXERCISE_PROGRAM_ID"='${P}'`),
    rows('SESSION_TEMPLATES', 'SESSION_TEMPLATE_ID', `x."COACH_ID"='${COACH}'`),
    rows('PROGRAM_TEMPLATES', 'PROGRAM_TEMPLATE_ID', `x."PROGRAM_TEMPLATE_COACH_ID"='${COACH}'`),
  ].join(' UNION ALL ') + ' ORDER BY 1;');
}
function actuals() {
  return sql(`SELECT "PROGRAM_EXERCISE_ID", "PROGRAM_EXERCISE_ACTUAL_SETS", "PROGRAM_EXERCISE_ACTUAL_REPS", "PROGRAM_EXERCISE_ACTUAL_WEIGHT_KG", "PROGRAM_EXERCISE_ACTUAL_RPE", "PROGRAM_EXERCISE_MEMBER_FEEDBACK" FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_PROGRAM_ID"='${P}' AND "PROGRAM_EXERCISE_ID" IN ('${X1}','${X2}','${X3}','${X4}') ORDER BY 1;`);
}

function show(label, r) {
  const j = r.json || {};
  let extra = '';
  if (r.status === 422) extra = ' errors=' + JSON.stringify(j.errors ?? j.details ?? j.error).slice(0, 600);
  else if (r.status === 409) extra = ' details=' + JSON.stringify(j.details);
  else if (r.status >= 400) extra = ' error=' + JSON.stringify(j.error);
  console.log(`${label.padEnd(34)} ${r.status}${extra}`);
  return r;
}

async function put(label, mutate, name) {
  const p = await getProgram(P);
  const req = buildRequest(p, name);
  mutate(req);
  return show(label, await api('PUT', `/training-programs/${P}`, req));
}
function findEx(req, id) {
  let found = null;
  const walk = bs => bs.forEach(b => { walk(b.blocks || []); (b.sessions || []).forEach(s => (s.circuits || []).forEach(c => c.exercises.forEach(e => { if (e && e.id === id) found = { e, c }; }))); });
  walk(req.body.blocks);
  return found;
}

const validCircuit = () => ({ name: 'l11fix-circuit', position: 1, mode: 'STANDARD', restAfterSeconds: 60,
  exercises: [{ exerciseId: OTHER_EXERCISE, position: 1, loadType: 'BODYWEIGHT', prescribedSets: 3, prescribedReps: 10 }] });
const sessionTemplate = (mut) => { const b = { name: 'l11fix-template', description: null, nature: 'STANDARD', sessionType: 'AUTONOME', circuits: [validCircuit()] }; mut && mut(b); return b; };

(async () => {
  const mode = process.argv[2];
  if (mode === 'nowrite') {
    console.log('== fingerprint BEFORE (count, md5 of rows + xmin)');
    const fpBefore = fingerprint(); console.log(fpBefore);

    console.log('\n== S1-b PUT coach/training-programs/{id}, expected 422');
    for (const v of ['nullBlocks', 'nullSessions', 'nullCircuits', 'nullExercises'])
      await put('S1-b PUT ' + v, req => applyVariant(req, v));

    console.log('\n== S1-b POST coach/session-templates, expected 422 (blocks/sessions do not exist on this body)');
    show('S1-b POST circuits:[null]', await api('POST', '/session-templates', sessionTemplate(b => { b.circuits = [null]; })));
    show('S1-b POST exercises:[null]', await api('POST', '/session-templates', sessionTemplate(b => { b.circuits[0].exercises = [null]; })));

    console.log('\n== S1-c POST coach/program-templates blocks:[null] (shared validator)');
    show('S1-c POST blocks:[null]', await api('POST', '/program-templates', { name: 'l11fix-pt', description: null, goal: 'GENERAL_FITNESS', durationWeeks: 1, body: { bodyVersion: 1, blocks: [null] } }));

    console.log('\n== S2-a, S2-b, S2-f (delete), expected 409');
    await put('S2-a change exerciseId of X1', req => { findEx(req, X1).e.exerciseId = OTHER_EXERCISE; });
    await put('S2-b X2 loadType KG -> BODYWEIGHT', req => { findEx(req, X2).e.loadType = 'BODYWEIGHT'; });
    await put('S2-f delete X4 (actuals)', req => { const f = findEx(req, X4); f.c.exercises = f.c.exercises.filter(e => e.id !== X4); });

    console.log('\n== fingerprint AFTER all non-writing proofs');
    const fpAfter = fingerprint(); console.log(fpAfter);
    console.log('\nS2-e / S1 no-write: fingerprints identical = ' + (fpBefore === fpAfter));
  } else if (mode === 'write') {
    console.log('== actuals BEFORE (id|sets|reps|kg|rpe|feedback)'); const a0 = actuals(); console.log(a0);

    console.log('\n== S1-d valid payloads unchanged');
    await put('S1-d PUT current tree unchanged (rename)', () => {}, 'l11fix-main');
    show('S1-d POST session-template valid', await api('POST', '/session-templates', sessionTemplate()));

    console.log('\n== S2-c same exercise, only prescribed reps X3 15 -> 16');
    await put('S2-c X3 prescribedReps 16', req => { findEx(req, X3).e.prescribedReps = 16; });
    const a1 = actuals(); console.log(a1);
    console.log('S2-c ACTUAL_* identical before and after = ' + (a0 === a1));
    console.log('S2-c X3 prescribed reps in DB: ' + sql(`SELECT "PROGRAM_EXERCISE_PRESCRIBED_REPS" FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_ID"='${X3}';`));

    console.log('\n== S2-d exercise without realised values changes exerciseId (N1)');
    await put('S2-d N1 exerciseId changed', req => { findEx(req, N1).e.exerciseId = OTHER_EXERCISE; });
    console.log('S2-d N1 exercise in DB is the new one = ' + (sql(`SELECT "PROGRAM_EXERCISE_EXERCISE_ID" FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_ID"='${N1}';`) === OTHER_EXERCISE));

    console.log('\n== S2-f clean reorder (swap N1 and N2 positions), expected 200');
    await put('S2-f reorder N1 <-> N2', req => { const a = findEx(req, N1).e, b = findEx(req, N2).e; [a.position, b.position] = [b.position, a.position]; });
    console.log('positions in DB (N1|N2): ' + sql(`SELECT "PROGRAM_EXERCISE_ID", "PROGRAM_EXERCISE_POSITION" FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_ID" IN ('${N1}','${N2}') ORDER BY 1;`).replace(/\n/g, ' ; '));

    const a2 = actuals();
    console.log('\nactuals AFTER the whole write group identical to BEFORE = ' + (a0 === a2));
    console.log('\n== fixture rows created after run start (session templates of coach A)');
    console.log(sql(`SELECT "SESSION_TEMPLATE_ID", "SESSION_TEMPLATE_NAME" FROM "SESSION_TEMPLATES" WHERE "COACH_ID"='${COACH}' AND "SESSION_TEMPLATE_CREATED_DATE" >= '${RUN_START}' ORDER BY 1;`));
  }
})();
