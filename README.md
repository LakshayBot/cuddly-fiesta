# World Engine

A persistent civilization simulation. Worlds, agents, decisions, and emergent behavior — built from explicit state changes, not generated storytelling.

> **Status:** Bootstrap phase. Only the `World` entity and the health check are implemented. Agents, simulation loop, events, and frontend screens arrive in subsequent phases.

---

## Architecture overview

```
.
├── docker-compose.yml          Postgres + API + Frontend (Vite dev server)
├── Dockerfile                  Multi-stage build for the API
├── WorldEngine.slnx            .NET solution (XML solution file, .NET 10 SDK)
├── src/
│   ├── WorldEngine.Api/        ASP.NET Core minimal-controller API
│   │   ├── Controllers/        WorldsController
│   │   ├── Contracts/          Request/response DTOs
│   │   └── Program.cs          Composition root, DI, migrations on boot
│   ├── WorldEngine.Domain/     Entities (World) and enums (SimulationStatus)
│   ├── WorldEngine.Infrastructure/
│   │   ├── Persistence/        WorldEngineDbContext + EF migrations
│   └── WorldEngine.Tests/      xUnit tests (in-memory DbContext)
└── frontend/
    ├── src/api/                Typed fetch client
    ├── src/components/         React components (Dashboard, etc.)
    ├── src/realtime/           SignalR client placeholder
    └── vite.config.ts          Vite + Tailwind + dev proxy to API
```

### Project rules in effect

- No premature abstraction layers — no repository pattern, no service factories, no mediator.
- The simulation engine is authoritative. Nothing mutates state from outside it.
- Events are produced by simulation actions, not generated as flavor text.
- Determinism where possible: world RNG is seeded; controllers never use ad-hoc randomness for behavior.

### Why these technology choices

| Concern             | Choice                                | Why |
|---------------------|---------------------------------------|-----|
| Backend framework   | .NET 10 Web API (target net10.0)      | Requested in brief; .NET 9 SDK was not installed locally, so we use the available .NET 10 SDK. Code patterns are API-compatible. |
| ORM                 | EF Core 9 + Npgsql provider           | Standard, mature, code-first migrations. |
| Database            | PostgreSQL 16                         | Relational store for events, relationships, inventories. |
| Real-time           | SignalR (added in a later phase)      | Browser push for sim ticks and events. |
| Frontend build      | Vite + React + TypeScript             | Fast dev loop; required by brief. |
| Styling             | Tailwind CSS v4                       | Required by brief; dark dashboard aesthetic. |
| Client state        | TanStack Query + Zustand              | Server cache vs. local UI state, separated. |
| Realtime client     | @microsoft/signalr                    | Matches backend SignalR. |

---

## Run locally (without Docker)

Prerequisites: .NET 10 SDK, Node.js 20+, PostgreSQL 16 reachable on `localhost:55432` (or change the connection string).

```bash
# 1. Start Postgres (any way you like). Example using the bundled compose service:
docker compose up -d postgres

# 2. Run the API. It applies migrations automatically on startup.
cd src/WorldEngine.Api
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://localhost:5080 \
dotnet run

# 3. In another shell, run the frontend dev server.
cd frontend
npm install
npm run dev
```

Open <http://localhost:5173>. The dashboard calls `/api/worlds` and `/health`, both proxied to the API by Vite.

The default connection string expects Postgres on `localhost:55432` to avoid clashing with a local Postgres on 5432. Override with:

```bash
export ConnectionStrings__WorldEngine='Host=localhost;Port=5432;Database=worldengine;Username=worldengine;Password=worldengine'
```

---

## Run with Docker Compose

```bash
docker compose up --build
```

Services:

| Service    | Host port | Notes                                                       |
|------------|-----------|-------------------------------------------------------------|
| postgres   | 55432     | Postgres 16 with healthcheck. Data persisted in named volume. |
| api        | 8080      | API image built from `Dockerfile`. Applies migrations on boot. |
| frontend   | 5173      | Vite dev server in Node 20 alpine. Source mounted as volume. |

The frontend container sets `VITE_API_TARGET=http://api:8080` so its dev-server proxy reaches the API container by service name.

Smoke-test the stack:

```bash
curl http://localhost:8080/health
curl -X POST http://localhost:8080/api/worlds -H 'Content-Type: application/json' -d '{"name":"My first world"}'
curl http://localhost:8080/api/worlds
```

---

## Endpoints (current)

| Method | Path                | Description                              |
|--------|---------------------|------------------------------------------|
| GET    | `/`                 | Service name and version                 |
| GET    | `/health`           | Liveness check                           |
| GET    | `/health/ready`     | Readiness check (verifies DB)            |
| GET    | `/api/worlds`       | List worlds, newest first                |
| GET    | `/api/worlds/{id}`  | Get a single world                       |
| POST   | `/api/worlds`       | Create a world (body: `{ "name": "..." }`) |

OpenAPI docs are exposed at `/openapi/v1.json` (and the Swagger UI equivalent in dev mode).

---

## Environment variables

| Variable                              | Used by        | Default                                  | Description |
|---------------------------------------|----------------|------------------------------------------|-------------|
| `ConnectionStrings__WorldEngine`      | API            | `Host=localhost;Port=55432;Database=worldengine;Username=worldengine;Password=worldengine` | PostgreSQL connection string. |
| `ASPNETCORE_ENVIRONMENT`              | API            | `Production` in container, `Development` locally | Standard ASP.NET Core env. |
| `ASPNETCORE_URLS`                     | API            | `http://+:8080` in container             | Override the binding URL. |
| `VITE_API_TARGET`                     | Frontend (Vite)| `http://localhost:5080` locally, `http://api:8080` in Docker | Dev-server proxy target. |

---

## Database

The first migration is `20260812154213_InitialWorld` in `src/WorldEngine.Infrastructure/Persistence/Migrations/`. It creates the `worlds` table with columns:

```
Id                  uuid         PK
Name                varchar(200) NOT NULL, indexed
RandomSeed          int          NOT NULL
CurrentSimulationTime timestamptz NOT NULL
SimulationSpeed     double       NOT NULL
Status              int          NOT NULL  (0=Uninitialized, 1=Paused, 2=Running, 3=Stopped)
TickNumber          bigint       NOT NULL
CreatedAt           timestamptz  NOT NULL
UpdatedAt           timestamptz  NOT NULL
```

The API applies pending migrations automatically on startup (`db.Database.Migrate()`). For a manual run:

```bash
dotnet ef database update \
  --project src/WorldEngine.Infrastructure \
  --startup-project src/WorldEngine.Api
```

Generate a new migration after entity changes:

```bash
dotnet ef migrations add <Name> \
  --project src/WorldEngine.Infrastructure \
  --startup-project src/WorldEngine.Api \
  --output-dir Persistence/Migrations
```

---

## Tests

```bash
dotnet test
```

The bootstrap ships with a single in-memory EF Core test verifying that a `World` can be persisted and read back. Real database tests will arrive alongside new entities.

---

## What's not built yet (intentional)

- Agents, settlements, locations, resources, relationships, needs, personalities, memories, goals, inventories.
- Simulation clock, tick loop, decision system, event log, SignalR hub.
- Inspector, timeline, statistics screens on the frontend.

These are sequenced for the next phases, not implemented now to keep the bootstrap verifiable.
---

## Production deployment

Live at **https://worldengine.lakshaycodes.dev** (API: https://worldengine-api.lakshaycodes.dev).

### Server layout (runbook: /opt/apps/WorldEngine)

| Piece | Value |
|---|---|
| Repo | `LakshayBot/cuddly-fiesta` (private) |
| Compose | `docker-compose.prod.yml` (checked in) |
| Config | `/opt/apps/WorldEngine/.env` (mode 600) |
| Caddy | `/opt/apps/proxy/Caddyfile` — `worldengine` → `worldengine-frontend:80`, `worldengine-api` → `worldengine-api:8080` |
| Cloudflare | Zone `lakshaycodes.dev` — proxied CNAMEs → tunnel `2b4a1622-fe2e-49a7-bf27-d9e9ebfa7750` |

### Production compose notes

- **No host-published ports** — everything routes via Docker networks + Caddy on `rag-network`.
- DB service is named `worldengine-db` (NOT `postgres`) — `postgres` is an alias taken by `rag-postgres` on the shared network; using it caused the API to auth against the wrong database.
- EF migrations run automatically on API boot (`db.Database.Migrate()`).
- The API applies CORS only when `Cors__Origins` is set (comma-separated origins).
- The frontend build takes `VITE_API_URL` / `VITE_HUB_URL` args (absolute production URLs).

### Redeploy

```bash
ssh sheep@sheep.tail5e5e2e.ts.net
cd /opt/apps/WorldEngine && git pull --rebase origin main
docker compose -f docker-compose.prod.yml build        # ~2 min
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml ps           # all healthy
```

### Validation

```bash
curl -s https://worldengine-api.lakshaycodes.dev/health
curl -s -o /dev/null -w '%{http_code}\n' https://worldengine.lakshaycodes.dev/
curl -s -X POST https://worldengine-api.lakshaycodes.dev/api/worlds \
  -H 'Content-Type: application/json' -d '{"name":"Smoke","initialPopulation":20}'
```

> **Gotchas hit during the initial deployment** (recorded for next time):
> 1. `.gitignore` excluded `Migrations/*Designer.cs` — those carry the `[Migration]` attributes; a fresh clone found **zero** migrations and created an empty `__EFMigrationsHistory` (API logged `relation "worlds" does not exist`). Designer files are now committed.
> 2. DNS negative caching: after creating the subdomains, the server and dev machines returned NXDOMAIN for up to ~30 min (SOA 1800s). Flush the resolver (`sudo resolvectl flush-caches`) or wait; the records are live on public resolvers immediately.
> 3. The ASP.NET runtime image has no `wget`/`curl` — the API healthcheck uses a bash TCP probe (`exec 3<>/dev/tcp/127.0.0.1/8080`).
> 4. nginx serves IPv4 only — frontend healthcheck must use `127.0.0.1`, not `localhost` (which resolves to `::1`).
