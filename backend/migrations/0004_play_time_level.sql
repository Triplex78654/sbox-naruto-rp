-- Colonnes pour le système de niveaux et de temps de jeu.
ALTER TABLE characters ADD COLUMN play_time INTEGER NOT NULL DEFAULT 0;
ALTER TABLE characters ADD COLUMN level INTEGER NOT NULL DEFAULT 1;
