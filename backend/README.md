# Backend Naruto RP (Cloudflare Worker + D1)

API appelée **uniquement par le serveur de jeu**, avec la clé serveur (`Authorization: Bearer <clé>`). Les joueurs n'y accèdent jamais.
Ce dossier n'est pas publié avec le jeu (vérifié avec l'aperçu de publication s&box).

## En local

```sh
cd backend
npm install
cp .dev.vars.example .dev.vars   # puis mettre une clé au choix
npm run dev                      # crée la base locale et lance l'API sur http://localhost:8787
npm test                         # base neuve + test de toutes les routes
```

Ajouter un membre du staff (local) :

```sh
npx wrangler d1 execute narutorp --local --command "INSERT INTO staff_members VALUES ('7656119XXXXXXXXXX','admin',0)"
```

## Routes (`/v1`)

| Méthode | Route | Rôle | Droit |
|---|---|---|---|
| GET | `/health` | test de vie (sans clé) | |
| GET | `/players/:steamId` | persos vivants + jutsus, ban actif, rang staff | serveur |
| POST | `/characters` | créer un perso (slot 1 ou 2, noms canon refusés) | serveur |
| PUT | `/characters/:id/state` | sauvegarde périodique (ryô) | serveur |
| POST | `/characters/:id/grade` | changer de grade (motif obligatoire, historique) | staff |
| POST | `/characters/:id/jutsus` | débloquer un jutsu | staff |
| DELETE | `/characters/:id/jutsus/:jutsuId?staffSteamId=…` | retirer un jutsu (sans corps) | staff |
| POST | `/characters/:id/medic` | statut médic-nin | staff |
| POST | `/characters/:id/clan` | valider (`approved: true`) ou refuser (retire le clan) un clan original | staff |
| POST | `/characters/:id/death` | mort RP (jamais automatique, loggée) | staff |
| GET | `/domains` | domaines de clan `[{ id, village, clan }]` | serveur |
| POST | `/domains/:id/clan` | attribuer (clan validé du même village) ou retirer (`clan: null`) un domaine | staff |
| POST | `/sanctions` | avertissement / kick (modérateur), ban temporaire (staff), ban permanent (admin) | selon type |
| POST | `/sanctions/:id/revoke` | lever une sanction (motif obligatoire) | staff |
| POST | `/logs` | log libre (combat, mise à terre…) ; types réservés au Worker refusés | serveur |

Les routes staff demandent `staffSteamId` : le serveur de jeu le remplit avec le SteamID du staff connecté, le Worker revérifie son rang dans `staff_members`. Un staff ne peut pas modifier son propre perso (sauf admin).

**À vérifier côté serveur de jeu** (le Worker n'a pas les fiches jutsus) : avant de débloquer un jutsu, que son rang est autorisé par le grade du perso et que sa nature / voie correspond.

## Mise en ligne (plus tard, compte Cloudflare gratuit)

1. `npx wrangler login`
2. `npx wrangler d1 create narutorp` → copier l'`database_id` dans `wrangler.toml`
3. `npx wrangler secret put SERVER_KEY` → une longue clé aléatoire, gardée aussi pour le serveur de jeu
4. `npm run deploy` (applique les migrations puis déploie)
5. Ajouter les admins : même commande que plus haut avec `--remote` au lieu de `--local`.
