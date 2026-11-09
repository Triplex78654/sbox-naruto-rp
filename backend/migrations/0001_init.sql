-- Schéma initial de la base Naruto RP (D1 / SQLite).
-- Toutes les dates sont en millisecondes depuis 1970 (Date.now()).
-- steam_id stocké en TEXT : un SteamID64 dépasse la précision des nombres JavaScript.

-- Membres du staff. Ajoutés à la main par un admin (wrangler d1 execute), pas par le jeu.
CREATE TABLE staff_members (
	steam_id   TEXT PRIMARY KEY,
	rank       TEXT NOT NULL CHECK (rank IN ('moderator', 'staff', 'admin')),
	added_at   INTEGER NOT NULL
);

-- Personnages : 2 emplacements par compte.
CREATE TABLE characters (
	id             INTEGER PRIMARY KEY AUTOINCREMENT,
	steam_id       TEXT NOT NULL,
	slot           INTEGER NOT NULL CHECK (slot IN (1, 2)),
	first_name     TEXT NOT NULL,
	last_name      TEXT NOT NULL,
	village        TEXT NOT NULL CHECK (village IN ('konoha', 'suna', 'kumo')),
	nature         TEXT NOT NULL CHECK (nature IN ('katon', 'futon', 'raiton', 'doton', 'suiton')),
	second_nature  TEXT CHECK (second_nature IN ('katon', 'futon', 'raiton', 'doton', 'suiton')),
	path           TEXT NOT NULL CHECK (path IN ('ninjutsu', 'taijutsu', 'kenjutsu', 'genjutsu', 'medical')),
	clan           TEXT,
	grade          TEXT NOT NULL DEFAULT 'eleve',
	is_medic       INTEGER NOT NULL DEFAULT 0,
	ryo            INTEGER NOT NULL DEFAULT 0 CHECK (ryo >= 0),
	appearance     TEXT NOT NULL DEFAULT '{}',
	-- Mort RP : jamais automatique, décidée par le staff. Le perso reste en base pour l'historique.
	dead_at        INTEGER,
	created_at     INTEGER NOT NULL,
	updated_at     INTEGER NOT NULL
);

-- Un seul perso vivant par emplacement.
CREATE UNIQUE INDEX characters_slot_alive ON characters (steam_id, slot) WHERE dead_at IS NULL;
CREATE INDEX characters_steam ON characters (steam_id);

-- Jutsus débloqués par le staff après apprentissage RP.
CREATE TABLE learned_jutsus (
	character_id   INTEGER NOT NULL REFERENCES characters (id),
	jutsu_id       TEXT NOT NULL,
	unlocked_by    TEXT NOT NULL,
	unlocked_at    INTEGER NOT NULL,
	PRIMARY KEY (character_id, jutsu_id)
);

-- Historique des changements de grade.
CREATE TABLE ranks_history (
	id             INTEGER PRIMARY KEY AUTOINCREMENT,
	character_id   INTEGER NOT NULL REFERENCES characters (id),
	old_grade      TEXT NOT NULL,
	new_grade      TEXT NOT NULL,
	staff_steam_id TEXT NOT NULL,
	reason         TEXT NOT NULL,
	created_at     INTEGER NOT NULL
);

-- Sanctions : motif obligatoire, historique conservé.
CREATE TABLE sanctions (
	id             INTEGER PRIMARY KEY AUTOINCREMENT,
	steam_id       TEXT NOT NULL,
	type           TEXT NOT NULL CHECK (type IN ('warn', 'kick', 'ban')),
	reason         TEXT NOT NULL,
	staff_steam_id TEXT NOT NULL,
	created_at     INTEGER NOT NULL,
	-- NULL = ban permanent (pour type = 'ban'), sans objet pour warn / kick.
	expires_at     INTEGER,
	revoked_at     INTEGER
);
CREATE INDEX sanctions_steam ON sanctions (steam_id);

-- Logs : combats, mises à terre, morts RP, argent, déblocages, actions staff.
CREATE TABLE logs (
	id               INTEGER PRIMARY KEY AUTOINCREMENT,
	type             TEXT NOT NULL,
	actor_steam_id   TEXT,
	target_steam_id  TEXT,
	data             TEXT NOT NULL DEFAULT '{}',
	created_at       INTEGER NOT NULL
);
CREATE INDEX logs_type_date ON logs (type, created_at);
CREATE INDEX logs_actor ON logs (actor_steam_id);
CREATE INDEX logs_target ON logs (target_steam_id);
