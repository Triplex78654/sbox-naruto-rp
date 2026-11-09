-- GDD : un clan original doit être validé par le staff.
-- 0 = en attente (ou sans clan), 1 = validé. Route de validation staff : tâche 11.
ALTER TABLE characters ADD COLUMN clan_validated INTEGER NOT NULL DEFAULT 0;
