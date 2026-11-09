// Test des routes : base locale neuve, Worker lancé avec `wrangler dev`, appels HTTP réels.
// Usage : npm test
import { spawn, execSync } from 'node:child_process';
import { rmSync } from 'node:fs';
import assert from 'node:assert/strict';

const PORT = 8799;
const KEY = 'cle-de-test';
const BASE = `http://127.0.0.1:${PORT}/v1`;
const STATE = '.wrangler/test-state';
const STAFF = '76561190000000001';
const MODO = '76561190000000002';
const PLAYER = '76561190000000003';

const wr = (args) => execSync(`npx wrangler ${args} --persist-to ${STATE}`, { stdio: 'pipe' });

rmSync(STATE, { recursive: true, force: true });
wr('d1 migrations apply narutorp --local');
wr(`d1 execute narutorp --local --command "INSERT INTO staff_members VALUES ('${STAFF}','staff',0),('${MODO}','moderator',0)"`);

const server = spawn(`npx wrangler dev --port ${PORT} --persist-to ${STATE} --var SERVER_KEY:${KEY}`, { shell: true, stdio: 'pipe' });
let serverLog = '';
server.stdout.on('data', d => (serverLog += d));
server.stderr.on('data', d => (serverLog += d));

async function call(method, path, body, key = KEY) {
	const res = await fetch(BASE + path, {
		method,
		headers: { 'content-type': 'application/json', ...(key ? { authorization: `Bearer ${key}` } : {}) },
		body: body ? JSON.stringify(body) : undefined,
	});
	return { status: res.status, body: await res.json() };
}

async function waitReady() {
	for (let i = 0; i < 60; i++) {
		try { if ((await fetch(`${BASE}/health`)).ok) return; } catch { }
		await new Promise(r => setTimeout(r, 500));
	}
	throw new Error('wrangler dev ne répond pas\n' + serverLog);
}

const tests = [];
const test = (name, fn) => tests.push({ name, fn });

const perso = { steamId: PLAYER, slot: 1, firstName: 'Ren', lastName: 'Kazuma', village: 'konoha', nature: 'raiton', path: 'ninjutsu' };
let id;

test('clé absente ou fausse → 401', async () => {
	assert.equal((await call('GET', `/players/${PLAYER}`, null, null)).status, 401);
	assert.equal((await call('GET', `/players/${PLAYER}`, null, 'mauvaise')).status, 401);
});

test('joueur inconnu → aucun perso', async () => {
	const r = await call('GET', `/players/${PLAYER}`);
	assert.equal(r.status, 200);
	assert.deepEqual(r.body.characters, []);
	assert.equal(r.body.activeBan, null);
});

test('création de perso', async () => {
	const r = await call('POST', '/characters', perso);
	assert.equal(r.status, 201, JSON.stringify(r.body));
	id = r.body.id;
});

test('emplacement déjà pris → 409', async () => {
	assert.equal((await call('POST', '/characters', perso)).status, 409);
});

test('nom canon, village inconnu, steamId faux → 400', async () => {
	assert.equal((await call('POST', '/characters', { ...perso, slot: 2, firstName: 'Naruto' })).status, 400);
	assert.equal((await call('POST', '/characters', { ...perso, slot: 2, lastName: 'Uchiha' })).status, 400);
	assert.equal((await call('POST', '/characters', { ...perso, slot: 2, village: 'kiri' })).status, 400);
	assert.equal((await call('POST', '/characters', { ...perso, slot: 3 })).status, 400);
	assert.equal((await call('POST', '/characters', { ...perso, steamId: '123' })).status, 400);
	assert.equal((await call('POST', '/characters', { ...perso, slot: 2, firstName: 'Naruto Uzumaki' })).status, 400);
	assert.equal((await call('POST', '/characters', { ...perso, slot: 2, firstName: 'Sasuke-kun' })).status, 400);
	assert.equal((await call('POST', '/characters', { ...perso, slot: 2, appearance: 'texte' })).status, 400);
});

test('un staff ne se valide pas lui-même', async () => {
	const r = await call('POST', '/characters', { ...perso, steamId: STAFF, clan: '' });
	assert.equal(r.status, 201, 'clan vide accepté');
	assert.equal((await call('POST', `/characters/${r.body.id}/grade`, { grade: 'kage', staffSteamId: STAFF, reason: 'moi-même' })).status, 403);
	assert.equal((await call('POST', `/characters/${r.body.id}/jutsus`, { jutsuId: 'katon_test', staffSteamId: STAFF })).status, 403);
});

test('sauvegarde des ryô, valeurs négatives refusées', async () => {
	assert.equal((await call('PUT', `/characters/${id}/state`, { ryo: 250 })).status, 200);
	assert.equal((await call('PUT', `/characters/${id}/state`, { ryo: -5 })).status, 400);
});

test('grade : modérateur refusé (403), staff accepté, motif obligatoire', async () => {
	assert.equal((await call('POST', `/characters/${id}/grade`, { grade: 'genin', staffSteamId: MODO, reason: 'examen réussi' })).status, 403);
	assert.equal((await call('POST', `/characters/${id}/grade`, { grade: 'genin', staffSteamId: STAFF })).status, 400);
	assert.equal((await call('POST', `/characters/${id}/grade`, { grade: 'genin', staffSteamId: STAFF, reason: 'examen réussi' })).status, 200);
	assert.equal((await call('POST', `/characters/${id}/grade`, { grade: 'hokage', staffSteamId: STAFF, reason: 'test' })).status, 400);
});

test('jutsus et médic débloqués par le staff', async () => {
	assert.equal((await call('POST', `/characters/${id}/jutsus`, { jutsuId: 'raiton_decharge', staffSteamId: STAFF })).status, 200);
	assert.equal((await call('POST', `/characters/${id}/jutsus`, { jutsuId: 'Bad Id!', staffSteamId: STAFF })).status, 400);
	assert.equal((await call('POST', `/characters/${id}/medic`, { isMedic: true, staffSteamId: STAFF })).status, 200);
	const r = await call('GET', `/players/${PLAYER}`);
	const c = r.body.characters[0];
	assert.equal(c.grade, 'genin');
	assert.equal(c.ryo, 250);
	assert.equal(c.is_medic, true);
	assert.deepEqual(c.jutsus, ['raiton_decharge']);
});

test('retrait de jutsu', async () => {
	assert.equal((await call('DELETE', `/characters/${id}/jutsus/raiton_decharge?staffSteamId=${STAFF}`)).status, 200);
	assert.deepEqual((await call('GET', `/players/${PLAYER}`)).body.characters[0].jutsus, []);
});

test('sanctions : modérateur peut avertir mais pas bannir ; ban actif renvoyé', async () => {
	assert.equal((await call('POST', '/sanctions', { type: 'warn', steamId: PLAYER, reason: 'HRP en zone RP', staffSteamId: MODO })).status, 201);
	assert.equal((await call('POST', '/sanctions', { type: 'ban', steamId: PLAYER, reason: 'triche', staffSteamId: MODO })).status, 403);
	assert.equal((await call('POST', '/sanctions', { type: 'ban', steamId: PLAYER, reason: 'triche', staffSteamId: STAFF, durationMinutes: 60 })).status, 201);
	const r = await call('GET', `/players/${PLAYER}`);
	assert.equal(r.body.activeBan.reason, 'triche');
});

test('ban permanent réservé à l\'admin ; levée de ban avec motif', async () => {
	assert.equal((await call('POST', '/sanctions', { type: 'ban', steamId: PLAYER, reason: 'triche', staffSteamId: STAFF })).status, 403);
	const banId = (await call('GET', `/players/${PLAYER}`)).body.activeBan.id;
	assert.equal((await call('POST', `/sanctions/${banId}/revoke`, { staffSteamId: STAFF })).status, 400);
	assert.equal((await call('POST', `/sanctions/${banId}/revoke`, { staffSteamId: STAFF, reason: 'erreur de cible' })).status, 200);
	assert.equal((await call('POST', `/sanctions/${banId}/revoke`, { staffSteamId: STAFF, reason: 'encore' })).status, 409);
	assert.equal((await call('GET', `/players/${PLAYER}`)).body.activeBan, null);
});

test('mort RP : staff + motif, le perso disparaît de la liste, emplacement libéré', async () => {
	assert.equal((await call('POST', `/characters/${id}/death`, { staffSteamId: STAFF, reason: 'mort en guerre déclarée' })).status, 200);
	assert.equal((await call('POST', `/characters/${id}/death`, { staffSteamId: STAFF, reason: 'encore' })).status, 409);
	assert.deepEqual((await call('GET', `/players/${PLAYER}`)).body.characters, []);
	assert.equal((await call('POST', '/characters', perso)).status, 201);
});

test('log libre et JSON invalide', async () => {
	assert.equal((await call('POST', '/logs', { type: 'downed', actorSteamId: STAFF, targetSteamId: PLAYER, data: { jutsu: 'test_zone' } })).status, 201);
	assert.equal((await call('POST', '/logs', { type: 'rp_death', data: {} })).status, 400, 'faux log staff refusé');
	assert.equal((await call('POST', '/logs', { type: 'sanction_ban', data: {} })).status, 400, 'faux log staff refusé');
	const res = await fetch(`${BASE}/logs`, { method: 'POST', headers: { authorization: `Bearer ${KEY}` }, body: '{oups' });
	assert.equal(res.status, 400);
});

test('route inconnue → 404', async () => {
	assert.equal((await call('GET', '/nimporte')).status, 404);
});

let failed = 0;
try {
	await waitReady();
	for (const t of tests) {
		try { await t.fn(); console.log(`ok   ${t.name}`); }
		catch (e) { failed++; console.log(`ÉCHEC ${t.name}\n     ${e.message}`); }
	}
} finally {
	// Sous Windows, tuer l'arbre de processus lancé par npx.
	if (process.platform === 'win32') { try { execSync(`taskkill /pid ${server.pid} /T /F`, { stdio: 'ignore' }); } catch { } }
	else server.kill();
}
console.log(failed ? `\n${failed} test(s) en échec` : `\n${tests.length} tests OK`);
process.exit(failed ? 1 : 0);
