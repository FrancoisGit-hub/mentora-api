// Runtime proofs of Lot 11.7. Usage: node proofs.js
// Prints statuses, property NAMES of the rest fields, 422 error keys/messages and SQL read results.
// Never prints a token, an OTP, an e-mail address or a whole body.
const { execFileSync } = require('child_process');
const { api, programOf, buildRequest, circuitsOf } = require('./put.js');

const MEMBER = '0d5608a9-d284-4346-a932-b38533c2c0b8';
const SEEDED_TEMPLATE = '24abe8d4-fd8d-4885-8c1b-2fcec1f3343c'; // "Prise de masse"
const FIXTURE_TEMPLATE = '11700000-0000-4000-8000-0000000000a1'; // l11rn fixture, circuits carry restAfterSeconds
const FIXTURE_SESSION_TEMPLATE = '11700000-0000-4000-8000-0000000000a2';
const CATALOG_EXERCISE = '72b9431c-9086-46ab-ba0e-2671f69702da';
const OLD = ['"restSeconds"', '"restAfterSeconds"'];

function sql(text) {
  return execFileSync('docker', ['exec', '-i', 'mentora-postgres-local', 'psql', '-U', 'mentora_user', '-d', 'mentora_db', '-At', '-v', 'ON_ERROR_STOP=1'],
    { input: text, encoding: 'utf8' }).trim();
}
const progExerciseRest = id => sql(`SELECT coalesce(string_agg(coalesce(v::text,'null'), ',' ORDER BY v NULLS LAST), '') FROM (SELECT "PROGRAM_EXERCISE_REST_SECONDS" v FROM "PROGRAM_EXERCISES" WHERE "PROGRAM_EXERCISE_PROGRAM_ID"='${id}') s;`);
const progCircuitRest = id => sql(`SELECT coalesce(string_agg(coalesce(v::text,'null'), ',' ORDER BY v NULLS LAST), '') FROM (SELECT "PROGRAM_CIRCUIT_REST_AFTER_SECONDS" v FROM "PROGRAM_CIRCUITS" WHERE "PROGRAM_CIRCUIT_PROGRAM_ID"='${id}') s;`);
const tplRest = (tid, key) => sql(`SELECT coalesce(string_agg(coalesce(v::text,'null'), ',' ORDER BY v NULLS LAST), '') FROM (SELECT (x #>> '{}')::int v FROM "PROGRAM_TEMPLATES" t, jsonb_path_query(t."PROGRAM_TEMPLATE_BODY", 'strict $.**.${key}', '{}', true) x WHERE t."PROGRAM_TEMPLATE_ID"='${tid}') s;`);
const tplRestCount = (tid, key) => sql(`SELECT count(*) FROM "PROGRAM_TEMPLATES" t, jsonb_path_query(t."PROGRAM_TEMPLATE_BODY", 'strict $.**.${key}', '{}', true) x WHERE t."PROGRAM_TEMPLATE_ID"='${tid}';`);

// Unique property names containing "rest" anywhere in a JSON value.
function restKeys(v, acc = new Set()) {
  if (Array.isArray(v)) v.forEach(x => restKeys(x, acc));
  else if (v && typeof v === 'object') for (const [k, x] of Object.entries(v)) { if (/rest/i.test(k)) acc.add(k); restKeys(x, acc); }
  return acc;
}
const names = v => [...restKeys(v)].sort().join(', ');
const hasOld = text => OLD.filter(t => text.includes(t));
function show422(label, r) {
  const e = r.json && (r.json.errors ?? r.json.details ?? r.json.error);
  console.log(`${label}: status ${r.status}\n   errors = ${JSON.stringify(e)}`);
}

(async () => {
  console.log('== PRE-CHECK member programs (any status) before the assign');
  console.log(sql(`SELECT "PROGRAM_STATUS", count(*) FROM "PROGRAMS" WHERE "PROGRAM_MEMBER_ID"='${MEMBER}' GROUP BY 1;`) || '(none)');
  const runStart = sql('SELECT now();');
  console.log('run time of this script (DB now()):', runStart);

  console.log('\n== A. assign the SEEDED template (Prise de masse)');
  const body = { templateId: SEEDED_TEMPLATE, name: 'l11rn-prog-seeded', goal: null, startDate: '2026-10-12', durationWeeks: null, body: null };
  const a = await api('POST', `/members/${MEMBER}/training-programs`, body);
  console.log('POST assign status', a.status, '| archivedProgramId =', a.json && a.json.data && a.json.data.archivedProgramId);
  if (a.status !== 201) { console.log(a.text.slice(0, 300)); process.exit(1); }
  const progA = programOf(a.json).programId ?? programOf(a.json).id;
  console.log('program A id:', progA);
  console.log('response rest property names :', names(a.json));
  console.log('old names in raw response    :', hasOld(a.text).length ? hasOld(a.text).join(' ') : 'none');
  console.log('exercise rest, template body (restSecondsAfterSeries, sorted):', tplRest(SEEDED_TEMPLATE, 'restSecondsAfterSeries'));
  console.log('exercise rest, PROGRAM_EXERCISE_REST_SECONDS (sorted)         :', progExerciseRest(progA));
  console.log('equal:', tplRest(SEEDED_TEMPLATE, 'restSecondsAfterSeries') === progExerciseRest(progA));
  console.log('circuit rest, template body restSecondsAfterExercices count:', tplRestCount(SEEDED_TEMPLATE, 'restSecondsAfterExercices'),
    '| PROGRAM_CIRCUIT_REST_AFTER_SECONDS:', progCircuitRest(progA) || '(no circuits)', '(seeded template carries no circuit rest)');

  console.log('\n== B. assign the FIXTURE template (circuit rest 60 and 120), archives program A');
  const b = await api('POST', `/members/${MEMBER}/training-programs`, { templateId: FIXTURE_TEMPLATE, name: 'l11rn-prog-fixture', goal: null, startDate: '2026-10-12', durationWeeks: null, body: null });
  console.log('POST assign status', b.status, '| archivedProgramId == program A:', b.json && b.json.data && b.json.data.archivedProgramId === progA);
  if (b.status !== 201) { console.log(b.text.slice(0, 300)); process.exit(1); }
  const progB = programOf(b.json).programId ?? programOf(b.json).id;
  console.log('program B id:', progB);
  console.log('response rest property names :', names(b.json));
  console.log('old names in raw response    :', hasOld(b.text).length ? hasOld(b.text).join(' ') : 'none');
  for (const [lbl, key, col] of [['exercise', 'restSecondsAfterSeries', progExerciseRest], ['circuit ', 'restSecondsAfterExercices', progCircuitRest]]) {
    const t = tplRest(FIXTURE_TEMPLATE, key), d = col(progB);
    console.log(`${lbl} rest: template body [${t}] | program column [${d}] | equal: ${t === d}`);
  }

  console.log('\n== C. GET program B back as the coach: property NAMES of the rest fields only');
  const g = await api('GET', `/training-programs/${progB}`);
  console.log('GET status', g.status);
  console.log('rest property names:', names(g.json));
  console.log('old names in raw response:', hasOld(g.text).length ? hasOld(g.text).join(' ') : 'none');

  console.log('\n== D. PUT training-programs with the NEW names (exercise 77, circuit 88 on the first of each)');
  const p0 = programOf(g.json);
  let req = buildRequest(p0, 'l11rn-prog-fixture');
  const cs = circuitsOf(req.body.blocks);
  cs[0].restSecondsAfterExercices = 88; cs[0].exercises[0].restSecondsAfterSeries = 77;
  console.log('before PUT  exercise [' + progExerciseRest(progB) + '] circuit [' + progCircuitRest(progB) + ']');
  const d = await api('PUT', `/training-programs/${progB}`, req);
  console.log('PUT status', d.status, '| response rest names:', names(d.json), '| old names in raw response:', hasOld(d.text).length ? hasOld(d.text).join(' ') : 'none');
  console.log('after PUT   exercise [' + progExerciseRest(progB) + '] circuit [' + progCircuitRest(progB) + ']');

  console.log('\n== E. PUT with the OLD names only (restSeconds 55, restAfterSeconds 99), new names omitted');
  const g2 = await api('GET', `/training-programs/${progB}`);
  req = buildRequest(programOf(g2.json), 'l11rn-prog-fixture');
  const cs2 = circuitsOf(req.body.blocks);
  delete cs2[0].restSecondsAfterExercices; cs2[0].restAfterSeconds = 99;
  delete cs2[0].exercises[0].restSecondsAfterSeries; cs2[0].exercises[0].restSeconds = 55;
  const e = await api('PUT', `/training-programs/${progB}`, req);
  console.log('PUT status', e.status);
  const ex = progExerciseRest(progB), ci = progCircuitRest(progB);
  console.log('after PUT   exercise [' + ex + '] circuit [' + ci + ']');
  console.log('value 55 stored:', ex.split(',').includes('55'), '| value 99 stored:', ci.split(',').includes('99'),
    '| (old names ignored, the omitted new names are stored as null)');

  console.log('\n== F. PUT with restSecondsAfterExercices = 601');
  const g3 = await api('GET', `/training-programs/${progB}`);
  req = buildRequest(programOf(g3.json), 'l11rn-prog-fixture');
  circuitsOf(req.body.blocks)[0].restSecondsAfterExercices = 601;
  const before = progCircuitRest(progB);
  show422('PUT 601', await api('PUT', `/training-programs/${progB}`, req));
  console.log('circuit column unchanged by the rejected PUT:', before === progCircuitRest(progB));

  console.log('\n== G. session template POST then GET with the new names');
  const st = { name: 'l11rn-st-new', description: null, nature: 'STANDARD', sessionType: 'AUTONOME', circuits: [
    { name: 'l11rn-circuit', position: 1, mode: 'STANDARD', restSecondsAfterExercices: 75,
      exercises: [{ exerciseId: CATALOG_EXERCISE, position: 1, loadType: 'BODYWEIGHT', prescribedSets: 3, prescribedReps: 10, restSecondsAfterSeries: 40 }] }] };
  const sp = await api('POST', '/session-templates', st);
  console.log('POST status', sp.status);
  const stId = sp.json && sp.json.data && (sp.json.data.id ?? sp.json.data.sessionTemplateId);
  console.log('session template id:', stId);
  const sg = await api('GET', `/session-templates/${stId}`);
  console.log('GET status', sg.status, '| rest property names:', names(sg.json), '| old names in raw response:', hasOld(sg.text).length ? hasOld(sg.text).join(' ') : 'none');
  console.log('stored content, new keys:', sql(`SELECT (length(t)-length(replace(t,'"restSecondsAfterSeries"','')))/length('"restSecondsAfterSeries"') || ' afterSeries, ' || (length(t)-length(replace(t,'"restSecondsAfterExercices"','')))/length('"restSecondsAfterExercices"') || ' afterExercices, ' || (length(t)-length(replace(t,'"restSeconds"','')))/length('"restSeconds"') || ' old restSeconds, ' || (length(t)-length(replace(t,'"restAfterSeconds"','')))/length('"restAfterSeconds"') || ' old restAfterSeconds' FROM (SELECT "SESSION_TEMPLATE_CONTENT"::text t FROM "SESSION_TEMPLATES" WHERE "SESSION_TEMPLATE_ID"='${stId}') s;`));

  console.log('\n== H. GET the MIGRATED fixture session template (a2) through the API');
  const mg = await api('GET', `/session-templates/${FIXTURE_SESSION_TEMPLATE}`);
  console.log('GET status', mg.status, '| rest property names:', names(mg.json), '| old names in raw response:', hasOld(mg.text).length ? hasOld(mg.text).join(' ') : 'none');

  console.log('\n== ids created by the HTTP tests (for the cleanup)');
  console.log('programs:', progA, progB, '| session template:', stId);
})().catch(err => { console.error('script error:', err.message); process.exit(1); });
