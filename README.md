# 🪳 CafardBizarre — Backend

> Jeu multijoueur coopératif inspiré de *Lethal Company*, dans lequel des joueurs incarnent des cafards devant voler des objets dans une poissonnerie sans se faire repérer.

---

## 📌 Description du projet

**CafardBizarre** est un jeu multijoueur développé avec **Unity (C#)** côté client, et un **backend Node.js** pour gérer l'authentification, les utilisateurs et les scores. L'authentification passe par **Steam** : aucun système de mot de passe n'est nécessaire, les joueurs se connectent directement avec leur compte Steam.

Ce dépôt contient **uniquement la partie backend** du projet.

---

## 🏗️ Stack technique

| Couche | Technologie |
|---|---|
| Runtime | Node.js (ESM) |
| Framework HTTP | Express 5 |
| Base de données | PostgreSQL |
| Authentification | Steam Web API + JWT |
| Sécurité | Helmet, CORS, jsonwebtoken |
| Développement | Nodemon |
| Validation | Zod |

---

## 📁 Architecture du projet

```
backendCafards/
└── backend/
    ├── .env                        # Variables d'environnement
    ├── package.json
    └── src/
        ├── server.js               # Point d'entrée — lance le serveur & vérifie la DB
        ├── app.js                  # Configuration Express & déclaration des routes
        ├── config/
        │   └── database.js         # Pool de connexion PostgreSQL (pg)
        ├── routes/
        │   ├── auth.routes.js      # POST /auth/steam, GET /auth/debug-token
        │   └── user.routes.js      # GET /user/me, GET /user/leaderboard
        ├── controllers/
        │   ├── auth.controller.js  # Logique du login Steam + génération JWT
        │   └── user.controller.js  # Retourne le profil de l'utilisateur connecté
        ├── services/
        │   ├── steam.service.js    # Appel à l'API Steam pour vérifier le ticket
        │   ├── jwt.service.js      # Génération et vérification des tokens JWT
        │   └── user.service.js     # Requêtes DB : findOrCreateUser
        └── middlewares/
            └── auth.middleware.js  # Vérification du JWT sur les routes protégées
```

---

## 🔐 Authentification Steam

Le système d'authentification fonctionne entièrement via **Steam Web API**, sans stockage de mot de passe.

### Flux complet

```
Client Unity
  │
  ├─ 1. SteamUser.GetAuthTicketForWebApi("unity")
  │       → Génère un ticket temporaire côté Steam
  │
  ├─ 2. POST /auth/steam  { ticket: "..." }
  │       → Envoi du ticket au backend
  │
  ├─ 3. steam.service.js → ISteamUserAuth/AuthenticateUserTicket
  │       → Validation du ticket par l'API officielle Steam
  │       → Retourne un steamId
  │
  ├─ 4. user.service.js → PostgreSQL
  │       → findOrCreateUser(steamId)
  │       → Crée l'utilisateur s'il n'existe pas encore
  │
  ├─ 5. jwt.service.js
  │       → Génère un JWT (durée : 7 jours)
  │
  └─ 6. Réponse au client
          {
            "user": { id, steam_id, highscore_money, highscore_days, created_at },
            "token": "<JWT>"
          }
```

Une fois le JWT obtenu, Unity l'inclut dans toutes ses requêtes suivantes via le header `Authorization: Bearer <token>`.

---

## 🗄️ Base de données PostgreSQL

### Schéma de la table `users`

```sql
CREATE TABLE users (
  id               SERIAL PRIMARY KEY,
  steam_id         VARCHAR UNIQUE NOT NULL,
  highscore_money  INT DEFAULT 0,
  highscore_days   INT DEFAULT 0,
  created_at       TIMESTAMP DEFAULT NOW()
);
```

| Colonne | Type | Description |
|---|---|---|
| `id` | SERIAL | Identifiant interne auto-incrémenté |
| `steam_id` | VARCHAR UNIQUE | Identifiant Steam du joueur |
| `highscore_money` | INT | Meilleur score en argent volé |
| `highscore_days` | INT | Meilleur nombre de jours survécus |
| `created_at` | TIMESTAMP | Date de première connexion |

---

## 🔗 Routes API

### Authentification — `/auth`

| Méthode | Route | Auth requise | Description |
|---|---|---|---|
| `POST` | `/auth/steam` | ❌ | Login via ticket Steam, retourne `user` + `token` |
| `GET` | `/auth/debug-token` | ❌ | Génère un token de test (dev uniquement) |

#### `POST /auth/steam`

**Body (JSON) :**
```json
{
  "ticket": "<hex ticket Steam>"
}
```

**Réponse (200) :**
```json
{
  "user": {
    "id": 1,
    "steam_id": "76561198XXXXXXXXX",
    "highscore_money": 0,
    "highscore_days": 0,
    "created_at": "2026-05-05T08:39:00.000Z"
  },
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Erreurs possibles :**
- `400` — Ticket manquant dans le body
- `401` — Ticket Steam invalide ou rejeté par l'API Steam

---

### Utilisateur — `/user`

| Méthode | Route | Auth requise | Description |
|---|---|---|---|
| `GET` | `/user/me` | ✅ JWT | Retourne le profil de l'utilisateur connecté |
| `GET` | `/user/leaderboard` | ❌ | Top 10 des joueurs par `highscore_money` |

#### `GET /user/me`

**Header requis :**
```
Authorization: Bearer <token>
```

**Réponse (200) :**
```json
{
  "id": 1,
  "steam_id": "76561198XXXXXXXXX",
  "highscore_money": 1200,
  "highscore_days": 5,
  "created_at": "2026-05-05T08:39:00.000Z"
}
```

#### `GET /user/leaderboard`

**Réponse (200) :**
```json
[
  { "id": 3, "steam_id": "...", "highscore_money": 9800, "highscore_days": 12 },
  { "id": 1, "steam_id": "...", "highscore_money": 1200, "highscore_days": 5 },
  ...
]
```

---

### Routes utilitaires

| Méthode | Route | Description |
|---|---|---|
| `GET` | `/` | Vérifie que le backend tourne (`Backend is running 🚀`) |
| `GET` | `/ping` | Health check JSON (`{ "message": "pong" }`) |

---

## ⚙️ Variables d'environnement

Créer un fichier `.env` à la racine de `backend/` :

```env
PORT=3000
JWT_SECRET=votre_secret_jwt_ici
STEAM_API_KEY=votre_cle_steam_ici
STEAM_APP_ID=votre_app_id_steam_ici
```

| Variable | Description |
|---|---|
| `PORT` | Port d'écoute du serveur (défaut : `3000`) |
| `JWT_SECRET` | Clé secrète pour signer les tokens JWT |
| `STEAM_API_KEY` | Clé API Steam (obtenue sur [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)) |
| `STEAM_APP_ID` | App ID de votre jeu Steam (utilisez `480` = SpaceWar pour les tests) |

> ⚠️ Ne jamais committer le fichier `.env` en production. Le `.gitignore` est déjà configuré pour l'ignorer.

---

## 🚀 Installation et démarrage

### Prérequis

- [Node.js](https://nodejs.org/) v18+
- [PostgreSQL](https://www.postgresql.org/) v14+
- Un compte Steam avec une clé API

### 1. Cloner le dépôt

```bash
git clone https://github.com/mateo809/CafardBizarre.git
cd CafardBizarre/backend
```

### 2. Installer les dépendances

```bash
npm install
```

### 3. Configurer la base de données

Créer la base et la table :

```sql
CREATE DATABASE cafards_db;

\c cafards_db

CREATE TABLE users (
  id               SERIAL PRIMARY KEY,
  steam_id         VARCHAR UNIQUE NOT NULL,
  highscore_money  INT DEFAULT 0,
  highscore_days   INT DEFAULT 0,
  created_at       TIMESTAMP DEFAULT NOW()
);
```

### 4. Configurer les variables d'environnement

```bash
cp .env.example .env
# Remplir les valeurs dans .env
```

### 5. Lancer le serveur (mode développement)

```bash
npm run dev
```

Le serveur démarre sur `http://localhost:3000`. La connexion à la base de données est vérifiée automatiquement au démarrage.

---

## 🔒 Sécurité

- **Helmet** — Sécurise les headers HTTP (XSS, clickjacking, etc.)
- **CORS** — Configuré pour autoriser les requêtes cross-origin
- **JWT** — Tokens signés, durée de validité de 7 jours
- **Tickets Steam temporaires** — Jamais stockés, validés à la volée
- **Variables sensibles** — Toutes dans `.env`, hors du dépôt git

---

## 🧩 Détail des modules

### `src/server.js`
Point d'entrée de l'application. Charge `dotenv`, démarre le serveur Express sur le `PORT` configuré, et effectue un `SELECT NOW()` pour vérifier que la base de données est accessible au démarrage.

### `src/app.js`
Configure l'instance Express : active `cors()`, `helmet()` et le parsing JSON. Monte les routes `/auth` et `/user`. Expose également `/` et `/ping` pour les health checks.

### `src/config/database.js`
Crée un pool de connexions PostgreSQL via le module `pg`. La configuration (hôte, port, nom de base, utilisateur, mot de passe) est définie ici — à terme, à déplacer vers des variables d'environnement.

### `src/services/steam.service.js`
Interroge l'endpoint `ISteamUserAuth/AuthenticateUserTicket/v1` de l'API Steam avec le ticket reçu du client Unity. Extrait et retourne le `steamId` si le ticket est valide, lève une erreur sinon.

### `src/services/user.service.js`
Fournit la fonction `findOrCreateUser(steamId)` : recherche l'utilisateur en base par son `steam_id`, ou le crée s'il n'existe pas encore. Retourne toujours un objet utilisateur complet.

### `src/services/jwt.service.js`
Encapsule `jsonwebtoken` pour exposer deux fonctions : `generateToken(payload)` (signe un JWT avec expiration à 7 jours) et `verifyToken(token)` (vérifie et décode un token).

### `src/middlewares/auth.middleware.js`
Middleware `authenticateToken` : extrait le JWT du header `Authorization: Bearer <token>`, le vérifie avec `JWT_SECRET`, et injecte le payload décodé dans `req.user`. Renvoie `401` si le header est absent, `403` si le token est invalide.

### `src/controllers/auth.controller.js`
Orchestre le flux de login Steam : récupère le ticket du body, appelle `verifySteamTicket`, puis `findOrCreateUser`, puis `generateToken`. Retourne `{ user, token }` au client.

### `src/controllers/user.controller.js`
Expose `getMe` : récupère l'utilisateur connecté en base via son `userId` (issu du JWT décodé par le middleware).

---

## 📊 État du projet

| Fonctionnalité | État |
|---|---|
| Serveur Express | ✅ Fonctionnel |
| Authentification Steam | ✅ Fonctionnel |
| Connexion PostgreSQL | ✅ Fonctionnel |
| JWT (login / vérification) | ✅ Fonctionnel |
| Route `GET /user/me` | ✅ Fonctionnel |
| Route `GET /user/leaderboard` | 🔄 En cours |
| Système de cosmétiques | ✅ Fonctionnel |
| Sauvegarde de parties | ⏳ Prévu |
| Tests unitaires | ⏳ Non commencé |
| Déploiement production | ⏳ Non commencé |

---

## 🤝 Contribution

Ce projet est développé dans le cadre d'un **CDA (Concepteur Développeur d'Applications)**. Les contributions externes ne sont pas ouvertes pour le moment.

---

## 📄 Licence

Projet éducatif — tous droits réservés.
