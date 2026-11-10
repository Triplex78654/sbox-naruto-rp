-- Domaines de clan : un refuge souterrain par domaine, attribué à un clan validé par le staff.
-- id = « <village>-<code> » (ex. konoha-D1) ; clan NULL = domaine libre.
CREATE TABLE domains (
	id TEXT PRIMARY KEY,
	village TEXT NOT NULL CHECK (village IN ('konoha','suna','kumo')),
	clan TEXT,
	assigned_by TEXT,
	assigned_at INTEGER
);
