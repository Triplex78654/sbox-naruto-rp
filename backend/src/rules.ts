// Règles de validation partagées par les routes. Le Worker revalide tout :
// le serveur de jeu n'est pas la seule barrière.

export const VILLAGES = ['konoha', 'suna', 'kumo'] as const;
export const NATURES = ['katon', 'futon', 'raiton', 'doton', 'suiton'] as const;
export const PATHS = ['ninjutsu', 'taijutsu', 'kenjutsu', 'genjutsu', 'medical'] as const;
// Ordre = progression. Des rangs propres au serveur pourront être insérés ici.
export const GRADES = ['eleve', 'genin', 'chunin', 'jonin', 'kage'] as const;
export const STAFF_RANKS = ['moderator', 'staff', 'admin'] as const;
export const SANCTION_TYPES = ['warn', 'kick', 'ban'] as const;

export type StaffRank = (typeof STAFF_RANKS)[number];

/** Rang de staff minimal par action. */
export const PERMISSIONS = {
	warn: 'moderator',
	kick: 'moderator',
	ban: 'staff',
	grade: 'staff',
	jutsu: 'staff',
	medic: 'staff',
	death: 'staff',
	revoke: 'staff',
	permanentBan: 'admin',
} as const satisfies Record<string, StaffRank>;

export function hasRank(rank: StaffRank | null, needed: StaffRank): boolean {
	return rank !== null && STAFF_RANKS.indexOf(rank) >= STAFF_RANKS.indexOf(needed);
}

/** SteamID64 : 17 chiffres. */
export function isSteamId(v: unknown): v is string {
	return typeof v === 'string' && /^\d{17}$/.test(v);
}

/** Identifiant de jutsu : snake_case comme dans les fiches .jutsu. */
export function isJutsuId(v: unknown): v is string {
	return typeof v === 'string' && /^[a-z0-9_]{2,64}$/.test(v);
}

export function oneOf<T extends string>(list: readonly T[], v: unknown): v is T {
	return typeof v === 'string' && (list as readonly string[]).includes(v);
}

/** Types de logs écrits par le Worker lui-même : interdits via POST /logs (pas de faux logs staff). */
export function isReservedLogType(type: string): boolean {
	return type.startsWith('sanction_') ||
		['character_created', 'grade_changed', 'jutsu_unlocked', 'jutsu_removed', 'medic_changed', 'rp_death'].includes(type);
}

/** Texte libre borné (motif, raison). */
export function isText(v: unknown, min = 3, max = 500): v is string {
	return typeof v === 'string' && v.trim().length >= min && v.length <= max;
}

// Noms de personnages et clans canon refusés à la création (GDD : aucun personnage canon).
// shortcut: liste courte des noms les plus connus ; à compléter par le staff au fil des refus.
const CANON = new Set([
	'naruto', 'sasuke', 'sakura', 'kakashi', 'itachi', 'madara', 'obito', 'minato', 'kushina', 'hinata',
	'neji', 'gaara', 'shikamaru', 'choji', 'ino', 'rock lee', 'lee', 'tenten', 'kiba', 'shino', 'jiraiya',
	'tsunade', 'orochimaru', 'hashirama', 'tobirama', 'hiruzen', 'danzo', 'kabuto', 'nagato', 'pain', 'konan',
	'kisame', 'deidara', 'sasori', 'hidan', 'kakuzu', 'zetsu', 'killer bee', 'bee', 'ay', 'temari', 'kankuro',
	'boruto', 'sarada', 'mitsuki', 'kawaki', 'himawari', 'guy', 'gai', 'asuma', 'kurenai', 'yamato', 'sai',
	'iruka', 'shisui', 'haku', 'zabuza', 'kaguya', 'hagoromo', 'indra', 'ashura', 'kurama', 'shukaku',
	// Clans canon : un clan original doit être validé par le staff.
	'uzumaki', 'uchiha', 'senju', 'hyuga', 'hyuuga', 'nara', 'akimichi', 'yamanaka', 'inuzuka', 'aburame',
	'sarutobi', 'hatake', 'otsutsuki', 'namikaze', 'hozuki', 'yuki',
]);

/** Nom ou prénom : lettres (accents, apostrophe, tiret), 2 à 20 caractères, pas canon. */
export function checkName(v: unknown): string | null {
	if (typeof v !== 'string') return 'nom manquant';
	const name = v.trim();
	if (!/^[\p{L}][\p{L}'\- ]{1,19}$/u.test(name)) return 'nom invalide (2 à 20 lettres)';
	// Chaque mot est testé : « Naruto Uzumaki » ou « Sasuke-kun » sont refusés.
	const lower = name.toLowerCase();
	if (CANON.has(lower) || lower.split(/[\s'\-]+/).some(w => CANON.has(w))) return 'nom de personnage ou clan canon interdit';
	return null;
}
