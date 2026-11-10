// API Naruto RP : appelée UNIQUEMENT par le serveur de jeu s&box, avec la clé serveur.
// Les joueurs n'y accèdent jamais.

import {
	GRADES, NATURES, PATHS, PERMISSIONS, SANCTION_TYPES, VILLAGES,
	checkName, hasRank, outranks, isJutsuId, isReservedLogType, isSteamId, isText, oneOf, type StaffRank,
} from './rules';

export interface Env {
	DB: D1Database;
	/** Secret Wrangler (`wrangler secret put SERVER_KEY`), jamais dans le repo. */
	SERVER_KEY: string;
}

type Json = Record<string, unknown>;

class HttpError extends Error {
	constructor(public status: number, message: string) { super(message); }
}

const json = (data: unknown, status = 200) =>
	new Response(JSON.stringify(data), { status, headers: { 'content-type': 'application/json; charset=utf-8' } });

/** Comparaison en temps constant pour ne pas révéler la clé par la durée de réponse. */
async function sameKey(a: string, b: string): Promise<boolean> {
	const enc = new TextEncoder();
	const [ha, hb] = await Promise.all([
		crypto.subtle.digest('SHA-256', enc.encode(a)),
		crypto.subtle.digest('SHA-256', enc.encode(b)),
	]);
	return crypto.subtle.timingSafeEqual(ha, hb);
}

async function readBody(req: Request): Promise<Json> {
	// Taille vérifiée sur le texte reçu (content-length peut manquer).
	const text = await req.text();
	if (text.length > 16_384) throw new HttpError(413, 'corps trop gros');
	let body: unknown;
	try { body = JSON.parse(text); } catch { throw new HttpError(400, 'JSON invalide'); }
	if (typeof body !== 'object' || body === null || Array.isArray(body)) throw new HttpError(400, 'objet JSON attendu');
	return body as Json;
}

function bad(message: string): never { throw new HttpError(400, message); }

async function staffRank(env: Env, steamId: string): Promise<StaffRank | null> {
	const row = await env.DB.prepare('SELECT rank FROM staff_members WHERE steam_id = ?').bind(steamId).first<{ rank: StaffRank }>();
	return row?.rank ?? null;
}

/** Vérifie que staffSteamId est bien membre du staff avec le rang requis. */
async function requireStaff(env: Env, staffSteamId: unknown, action: keyof typeof PERMISSIONS) {
	if (!isSteamId(staffSteamId)) bad('staffSteamId invalide');
	const rank = await staffRank(env, staffSteamId);
	if (!hasRank(rank, PERMISSIONS[action])) throw new HttpError(403, `rang staff insuffisant pour « ${action} »`);
	return { staff: staffSteamId, rank: rank as StaffRank };
}

/** Un staff ne valide pas son propre perso (sauf admin). */
function notSelf(s: { staff: string; rank: StaffRank }, c: Json) {
	if (s.staff === String(c.steam_id) && s.rank !== 'admin') throw new HttpError(403, 'un staff ne peut pas modifier son propre perso');
}

/** Perso vivant, sinon 409. */
async function getAliveCharacter(env: Env, id: number) {
	const c = await getCharacter(env, id);
	if (c.dead_at != null) throw new HttpError(409, 'perso mort');
	return c;
}

async function getCharacter(env: Env, id: number) {
	const c = await env.DB.prepare('SELECT * FROM characters WHERE id = ?').bind(id).first<Json>();
	if (!c) throw new HttpError(404, 'perso introuvable');
	return c;
}

function logStmt(env: Env, type: string, actor: string | null, target: string | null, data: Json) {
	return env.DB.prepare('INSERT INTO logs (type, actor_steam_id, target_steam_id, data, created_at) VALUES (?, ?, ?, ?, ?)')
		.bind(type, actor, target, JSON.stringify(data), Date.now());
}

function characterId(raw: string): number {
	const id = Number(raw);
	if (!Number.isSafeInteger(id) || id <= 0) bad('id de perso invalide');
	return id;
}

// ─── Routes ──────────────────────────────────────────────────────────────────

/** Tout ce dont le serveur a besoin à la connexion d'un joueur. */
async function getPlayer(env: Env, steamId: string) {
	if (!isSteamId(steamId)) bad('steamId invalide');
	const now = Date.now();
	const [chars, jutsus, ban, rank] = await Promise.all([
		env.DB.prepare('SELECT * FROM characters WHERE steam_id = ? AND dead_at IS NULL ORDER BY slot').bind(steamId).all<Json>(),
		env.DB.prepare(`SELECT lj.character_id, lj.jutsu_id FROM learned_jutsus lj
			JOIN characters c ON c.id = lj.character_id WHERE c.steam_id = ? AND c.dead_at IS NULL`).bind(steamId).all<{ character_id: number; jutsu_id: string }>(),
		env.DB.prepare(`SELECT id, reason, expires_at FROM sanctions WHERE steam_id = ? AND type = 'ban'
			AND revoked_at IS NULL AND (expires_at IS NULL OR expires_at > ?) ORDER BY created_at DESC LIMIT 1`).bind(steamId, now).first<Json>(),
		staffRank(env, steamId),
	]);
	return {
		steamId,
		staffRank: rank,
		activeBan: ban ?? null,
		characters: chars.results.map(c => ({
			...c,
			is_medic: c.is_medic === 1,
			clan_validated: c.clan_validated === 1,
			appearance: JSON.parse(String(c.appearance)),
			jutsus: jutsus.results.filter(j => j.character_id === c.id).map(j => j.jutsu_id),
		})),
	};
}

async function createCharacter(env: Env, b: Json) {
	if (!isSteamId(b.steamId)) bad('steamId invalide');
	if (b.slot !== 1 && b.slot !== 2) bad('slot doit valoir 1 ou 2');
	for (const k of ['firstName', 'lastName'] as const) {
		const err = checkName(b[k]);
		if (err) bad(`${k} : ${err}`);
	}
	if (!oneOf(VILLAGES, b.village)) bad('village invalide');
	if (!oneOf(NATURES, b.nature)) bad('nature invalide');
	if (!oneOf(PATHS, b.path)) bad('voie invalide');
	// Clan : vide (sans clan) ou original, validé ensuite par le staff.
	const clan = b.clan == null || String(b.clan).trim() === '' ? null : String(b.clan).trim();
	if (clan !== null && checkName(clan)) bad(`clan : ${checkName(clan)}`);
	if (b.appearance != null && (typeof b.appearance !== 'object' || Array.isArray(b.appearance))) bad('appearance doit être un objet');
	const appearance = JSON.stringify(b.appearance ?? {});
	if (appearance.length > 4000) bad('apparence trop grosse');

	const now = Date.now();
	try {
		// Perso et log dans la même transaction (batch D1).
		const [res] = await env.DB.batch([
			env.DB.prepare(`INSERT INTO characters
				(steam_id, slot, first_name, last_name, village, nature, path, clan, appearance, created_at, updated_at)
				VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`)
				.bind(b.steamId, b.slot, String(b.firstName).trim(), String(b.lastName).trim(), b.village, b.nature, b.path,
					clan, appearance, now, now),
			env.DB.prepare(`INSERT INTO logs (type, actor_steam_id, target_steam_id, data, created_at)
				VALUES ('character_created', ?, ?, json_object('characterId', last_insert_rowid(), 'slot', ?), ?)`)
				.bind(b.steamId, b.steamId, b.slot, now),
		]);
		return json({ id: res.meta.last_row_id }, 201);
	} catch (e) {
		if (String(e).includes('UNIQUE')) throw new HttpError(409, 'emplacement déjà occupé');
		throw e;
	}
}

/** Sauvegarde périodique de l'état de jeu (pas les grades ni les jutsus : réservés au staff). */
async function saveState(env: Env, id: number, b: Json) {
	await getAliveCharacter(env, id);
	if (!Number.isSafeInteger(b.ryo) || (b.ryo as number) < 0 || (b.ryo as number) > 1_000_000_000) bad('ryo invalide');
	await env.DB.prepare('UPDATE characters SET ryo = ?, updated_at = ? WHERE id = ? AND dead_at IS NULL').bind(b.ryo, Date.now(), id).run();
	return json({ ok: true });
}

async function setGrade(env: Env, id: number, b: Json) {
	const s = await requireStaff(env, b.staffSteamId, 'grade');
	const staff = s.staff;
	if (!oneOf(GRADES, b.grade)) bad('grade invalide');
	if (!isText(b.reason)) bad('motif obligatoire (3 à 500 caractères)');
	const c = await getAliveCharacter(env, id);
	notSelf(s, c);
	const now = Date.now();
	await env.DB.batch([
		env.DB.prepare('UPDATE characters SET grade = ?, updated_at = ? WHERE id = ? AND dead_at IS NULL').bind(b.grade, now, id),
		env.DB.prepare('INSERT INTO ranks_history (character_id, old_grade, new_grade, staff_steam_id, reason, created_at) VALUES (?, ?, ?, ?, ?, ?)')
			.bind(id, c.grade, b.grade, staff, b.reason, now),
		logStmt(env, 'grade_changed', staff, String(c.steam_id), { characterId: id, from: c.grade, to: b.grade, reason: b.reason }),
	]);
	return json({ ok: true });
}

async function unlockJutsu(env: Env, id: number, b: Json) {
	const s = await requireStaff(env, b.staffSteamId, 'jutsu');
	const staff = s.staff;
	if (!isJutsuId(b.jutsuId)) bad('jutsuId invalide');
	const c = await getAliveCharacter(env, id);
	notSelf(s, c);
	await env.DB.batch([
		env.DB.prepare('INSERT OR IGNORE INTO learned_jutsus (character_id, jutsu_id, unlocked_by, unlocked_at) VALUES (?, ?, ?, ?)')
			.bind(id, b.jutsuId, staff, Date.now()),
		logStmt(env, 'jutsu_unlocked', staff, String(c.steam_id), { characterId: id, jutsuId: b.jutsuId }),
	]);
	return json({ ok: true });
}

async function removeJutsu(env: Env, id: number, jutsuId: string, staffSteamId: unknown) {
	const s = await requireStaff(env, staffSteamId, 'jutsu');
	const staff = s.staff;
	if (!isJutsuId(jutsuId)) bad('jutsuId invalide');
	const c = await getAliveCharacter(env, id);
	notSelf(s, c);
	await env.DB.batch([
		env.DB.prepare('DELETE FROM learned_jutsus WHERE character_id = ? AND jutsu_id = ?').bind(id, jutsuId),
		logStmt(env, 'jutsu_removed', staff, String(c.steam_id), { characterId: id, jutsuId }),
	]);
	return json({ ok: true });
}

async function setMedic(env: Env, id: number, b: Json) {
	const s = await requireStaff(env, b.staffSteamId, 'medic');
	const staff = s.staff;
	if (typeof b.isMedic !== 'boolean') bad('isMedic doit être true ou false');
	const c = await getAliveCharacter(env, id);
	notSelf(s, c);
	await env.DB.batch([
		env.DB.prepare('UPDATE characters SET is_medic = ?, updated_at = ? WHERE id = ? AND dead_at IS NULL').bind(b.isMedic ? 1 : 0, Date.now(), id),
		logStmt(env, 'medic_changed', staff, String(c.steam_id), { characterId: id, isMedic: b.isMedic }),
	]);
	return json({ ok: true });
}

/** Validation (ou refus) d'un clan original par le staff. Refuser retire le clan. */
async function validateClan(env: Env, id: number, b: Json) {
	const s = await requireStaff(env, b.staffSteamId, 'clan');
	if (typeof b.approved !== 'boolean') bad('approved doit être true ou false');
	const c = await getAliveCharacter(env, id);
	notSelf(s, c);
	if (c.clan == null) throw new HttpError(409, "ce perso n'a pas de clan");
	await env.DB.batch([
		b.approved
			? env.DB.prepare('UPDATE characters SET clan_validated = 1, updated_at = ? WHERE id = ? AND dead_at IS NULL').bind(Date.now(), id)
			: env.DB.prepare('UPDATE characters SET clan = NULL, clan_validated = 0, updated_at = ? WHERE id = ? AND dead_at IS NULL').bind(Date.now(), id),
		logStmt(env, 'clan_validated', s.staff, String(c.steam_id), { characterId: id, clan: c.clan, approved: b.approved }),
	]);
	return json({ ok: true });
}

/** Liste des domaines de clan (un domaine libre a clan = null). */
async function listDomains(env: Env) {
	const { results } = await env.DB.prepare('SELECT id, village, clan FROM domains ORDER BY id').all();
	return json(results);
}

/** Attribue (ou retire, clan = null) un domaine. Le clan doit être validé dans le même village. */
async function setDomainClan(env: Env, id: string, b: Json) {
	const s = await requireStaff(env, b.staffSteamId, 'clan');
	if (!oneOf(VILLAGES, b.village)) bad('village invalide');
	if (!/^[a-z]+-[A-Za-z0-9_]{1,16}$/.test(id) || !id.startsWith(`${b.village}-`)) bad("l'id du domaine doit commencer par « village- »");
	let clan: string | null = null;
	if (b.clan != null) {
		if (typeof b.clan !== 'string' || b.clan.trim() === '') bad('clan invalide');
		clan = b.clan.trim();
		const ok = await env.DB.prepare(
			'SELECT 1 FROM characters WHERE clan = ? AND clan_validated = 1 AND village = ? AND dead_at IS NULL LIMIT 1')
			.bind(clan, b.village).first();
		if (!ok) throw new HttpError(409, "clan non validé dans ce village");
	}
	const now = Date.now();
	await env.DB.batch([
		env.DB.prepare(`INSERT INTO domains (id, village, clan, assigned_by, assigned_at) VALUES (?, ?, ?, ?, ?)
			ON CONFLICT(id) DO UPDATE SET clan = excluded.clan, assigned_by = excluded.assigned_by, assigned_at = excluded.assigned_at`)
			.bind(id, b.village, clan, s.staff, now),
		logStmt(env, 'domain_assigned', s.staff, null, { domainId: id, clan }),
	]);
	return json({ ok: true });
}

/** Mort RP : jamais automatique, toujours décidée par le staff et loggée. */
async function killCharacter(env: Env, id: number, b: Json) {
	const { staff } = await requireStaff(env, b.staffSteamId, 'death');
	if (!isText(b.reason)) bad('motif obligatoire (3 à 500 caractères)');
	const c = await getCharacter(env, id);
	if (c.dead_at != null) throw new HttpError(409, 'perso déjà mort');
	const now = Date.now();
	await env.DB.batch([
		env.DB.prepare('UPDATE characters SET dead_at = ?, updated_at = ? WHERE id = ? AND dead_at IS NULL').bind(now, now, id),
		logStmt(env, 'rp_death', staff, String(c.steam_id), { characterId: id, reason: b.reason }),
	]);
	return json({ ok: true });
}

async function addSanction(env: Env, b: Json) {
	if (!oneOf(SANCTION_TYPES, b.type)) bad('type de sanction invalide');
	// Ban sans durée = permanent, réservé à l'admin (le GDD prévoit des bans temporaires).
	const permanent = b.type === 'ban' && b.durationMinutes == null;
	const { staff, rank } = await requireStaff(env, b.staffSteamId, permanent ? 'permanentBan' : b.type);
	if (!isSteamId(b.steamId)) bad('steamId invalide');
	if (!isText(b.reason)) bad('motif obligatoire (3 à 500 caractères)');
	// Hiérarchie : pas de sanction sur soi ni sur un staff de rang égal ou supérieur.
	if (b.steamId === staff) throw new HttpError(403, 'impossible de se sanctionner soi-même');
	const targetRank = await staffRank(env, b.steamId);
	if (targetRank && !outranks(rank, targetRank)) throw new HttpError(403, 'impossible de sanctionner un staff de rang égal ou supérieur');
	let expires: number | null = null;
	if (b.type === 'ban' && !permanent) {
		if (!Number.isSafeInteger(b.durationMinutes) || (b.durationMinutes as number) <= 0) bad('durationMinutes invalide');
		expires = Date.now() + (b.durationMinutes as number) * 60_000;
	}
	const now = Date.now();
	const [res] = await env.DB.batch([
		env.DB.prepare('INSERT INTO sanctions (steam_id, type, reason, staff_steam_id, created_at, expires_at) VALUES (?, ?, ?, ?, ?, ?)')
			.bind(b.steamId, b.type, b.reason, staff, now, expires),
		logStmt(env, `sanction_${b.type}`, staff, b.steamId, { reason: b.reason, expiresAt: expires }),
	]);
	return json({ id: res.meta.last_row_id }, 201);
}

/** Lever une sanction, avec motif et log. */
async function revokeSanction(env: Env, id: number, b: Json) {
	const { staff } = await requireStaff(env, b.staffSteamId, 'revoke');
	if (!isText(b.reason)) bad('motif obligatoire (3 à 500 caractères)');
	const s = await env.DB.prepare('SELECT steam_id, revoked_at FROM sanctions WHERE id = ?').bind(id).first<Json>();
	if (!s) throw new HttpError(404, 'sanction introuvable');
	if (s.revoked_at != null) throw new HttpError(409, 'sanction déjà levée');
	await env.DB.batch([
		env.DB.prepare('UPDATE sanctions SET revoked_at = ? WHERE id = ? AND revoked_at IS NULL').bind(Date.now(), id),
		logStmt(env, 'sanction_revoked', staff, String(s.steam_id), { sanctionId: id, reason: b.reason }),
	]);
	return json({ ok: true });
}

async function addLog(env: Env, b: Json) {
	if (typeof b.type !== 'string' || !/^[a-z_]{2,40}$/.test(b.type)) bad('type de log invalide');
	if (isReservedLogType(b.type)) bad('type de log réservé au Worker');
	if (b.actorSteamId != null && !isSteamId(b.actorSteamId)) bad('actorSteamId invalide');
	if (b.targetSteamId != null && !isSteamId(b.targetSteamId)) bad('targetSteamId invalide');
	const data = (b.data ?? {}) as Json;
	if (JSON.stringify(data).length > 4000) bad('data trop gros');
	await logStmt(env, b.type, (b.actorSteamId as string) ?? null, (b.targetSteamId as string) ?? null, data).run();
	return json({ ok: true }, 201);
}

// ─── Aiguillage ──────────────────────────────────────────────────────────────

async function route(req: Request, env: Env): Promise<Response> {
	const url = new URL(req.url);
	const parts = url.pathname.split('/').filter(Boolean); // ex. ['v1', 'characters', '12', 'grade']
	if (parts[0] !== 'v1') throw new HttpError(404, 'route inconnue');
	const [, res, idRaw, sub, subId] = parts;
	const m = req.method;

	if (res === 'health' && m === 'GET') return json({ ok: true });

	// Toutes les autres routes exigent la clé serveur.
	const auth = req.headers.get('authorization') ?? '';
	if (!env.SERVER_KEY || !auth.startsWith('Bearer ') || !(await sameKey(auth.slice(7), env.SERVER_KEY)))
		throw new HttpError(401, 'clé serveur invalide');

	if (res === 'players' && idRaw && !sub && m === 'GET') return json(await getPlayer(env, idRaw));
	if (res === 'characters' && !idRaw && m === 'POST') return createCharacter(env, await readBody(req));
	if (res === 'sanctions' && !idRaw && m === 'POST') return addSanction(env, await readBody(req));
	if (res === 'sanctions' && idRaw && sub === 'revoke' && m === 'POST') return revokeSanction(env, characterId(idRaw), await readBody(req));
	if (res === 'domains' && !idRaw && m === 'GET') return listDomains(env);
	if (res === 'domains' && idRaw && sub === 'clan' && m === 'POST') return setDomainClan(env, idRaw, await readBody(req));
	if (res === 'logs' && !idRaw && m === 'POST') return addLog(env, await readBody(req));

	if (res === 'characters' && idRaw) {
		const id = characterId(idRaw);
		if (sub === 'state' && m === 'PUT') return saveState(env, id, await readBody(req));
		if (sub === 'grade' && m === 'POST') return setGrade(env, id, await readBody(req));
		if (sub === 'medic' && m === 'POST') return setMedic(env, id, await readBody(req));
		if (sub === 'clan' && m === 'POST') return validateClan(env, id, await readBody(req));
		if (sub === 'death' && m === 'POST') return killCharacter(env, id, await readBody(req));
		if (sub === 'jutsus' && !subId && m === 'POST') return unlockJutsu(env, id, await readBody(req));
		// DELETE sans corps (pas garanti par tous les clients HTTP) : staff en paramètre d'URL.
		if (sub === 'jutsus' && subId && m === 'DELETE') return removeJutsu(env, id, subId, url.searchParams.get('staffSteamId'));
	}
	throw new HttpError(404, 'route inconnue');
}

export default {
	async fetch(req: Request, env: Env): Promise<Response> {
		try {
			return await route(req, env);
		} catch (e) {
			if (e instanceof HttpError) return json({ error: e.message }, e.status);
			console.error(e);
			return json({ error: 'erreur interne' }, 500);
		}
	},
} satisfies ExportedHandler<Env>;
