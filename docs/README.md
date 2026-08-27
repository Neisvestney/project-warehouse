# Project Warehouse — Documentation

A warehouse management web app with barcode/QR scanning (PWA), user management, and a role-based permission system.

## Architecture

```
projectwarehouse.client/   React 19 + TypeScript PWA (Vite)
ProjectWarehouse.Server/   ASP.NET Core 10 REST API
  ├── Controllers/         HTTP layer
  ├── Services/            Business logic
  ├── Domain/              EF Core entities
  ├── Infrastructure/      Auth handlers, permissions, error helpers
  ├── Models/              Request/response DTOs
  ├── Data/                DbContext, migrations, seeder
  └── Migrations/          EF Core migrations
```

**Backend:** ASP.NET Core 10, Entity Framework Core, PostgreSQL, ASP.NET Core Identity, JWT Bearer auth, Quartz, SixLabors.ImageSharp  
**Frontend:** React 19, TypeScript 6, Vite 8, MUI v9, React Router v7, PWA (vite-plugin-pwa), zxing-wasm

## Docs Index

**These docs describe only what the code cannot.** Endpoints, DTOs, enums, permission strings and error codes
are read from the source — the controllers' XML `<remarks>`, `projectwarehouse.client/src/api/types.gen.ts`, and
the Scalar UI at `/scalar`. Nothing derivable is mirrored here, because a mirror rots. What lives in these files
is rationale, invariants, cross-cutting conventions and decisions.

**Conventions and cross-cutting**

| File | Contents |
|------|----------|
| [api.md](api.md) | API conventions (pagination, filtering, day boundaries and `X-Time-Zone`), JWT auth, refresh rotation, SecurityVersion invalidation |
| [permissions.md](permissions.md) | The `_assigned` convention, notable access rules, where access is checked, RBAC + direct permissions |
| [errors.md](errors.md) | `AppProblemDetails` envelope, field-path conventions, persisted errors, controller helpers |
| [validation.md](validation.md) | Validation pipeline, `[JsonRequired]`, ModelState mapping |
| [backend-patterns.md](backend-patterns.md) | Recurring backend patterns — search, inheritable fields, list sync, background work, access rules, enums |
| [changelog.md](changelog.md) | Changelog system — how to add tracking to a method, Action/ActionData |
| [observability-specification.md](observability-specification.md) | Telemetry — OpenTelemetry traces and logs, file archive on prod, local analysis stack |
| [backlog.md](backlog.md) | Deferred work — what is blocked, why, and the event that unblocks it |

**Frontend**

| File | Contents |
|------|----------|
| [frontend.md](frontend.md) | Architecture: tech stack, directory layering, routing, cross-cutting conventions, pages, providers, PWA, API client |
| [frontend-components.md](frontend-components.md) | Component reference grouped by domain — catalog, files, marketplace, orders, warehouse, forms |
| [frontend-state.md](frontend-state.md) | How state enters and leaves components — URL state hooks, form hooks, `ObservableForm` |
| [frontend-realtime.md](frontend-realtime.md) | `RealtimeProvider`, subscription hooks, presence, edit-lock and stale-data UI |
| [native-client.md](native-client.md) | Capacitor build — predefined servers, hardware scanner plugin, native caveats |

**Domain specifications**

| File | Contents |
|------|----------|
| [technical-specification.md](technical-specification.md) | WMS operational flows — receipts, transfers, write-offs, stocktakes |
| [orders-specification.md](orders-specification.md) | Orders — FBS, FBO, Direct, assembly tasks, status flows |
| [items-specification.md](items-specification.md) | Catalog items — types, `FullName`, inheritance, tags, images, listing rules |
| [stock-forecast-specification.md](stock-forecast-specification.md) | Прогноз остатков — расчёт «на сколько дней хватит», окно расхода, пороги предупреждения |
| [marketplaces-specification.md](marketplaces-specification.md) | Integration platform — Ozon Seller API, client codegen, credential storage, warehouse/card sync & mapping |
| [marketplaces-orders-fbs-specification.md](marketplaces-orders-fbs-specification.md) | FBS order sync — posting discovery, status catch-up, order creation, label retrieval |
| [realtime-specification.md](realtime-specification.md) | Real-time transport — SSE, event schema, watch registry, advisory edit locks |
| [data-files-specification.md](data-files-specification.md) | File storage — upload, storage abstraction, FK attachments, orphan GC, serving rules |

### Licence note

Image resizing uses **SixLabors.ImageSharp 3.x**, under the Six Labors Split License: free for organizations
under $1M annual revenue, commercial licence required above it. If that threshold is crossed, the alternative is
SkiaSharp (MIT), which needs native Linux assets in the image.

## Local Dev Setup

### Prerequisites

- .NET 10 SDK
- Node.js 20+
- PostgreSQL (local or via Docker)

### Environment Variables (required before first run)

Copy `.env.example` to `.env` at the repo root and fill in values:

```
cp .env.example .env
```

| Variable | Description | Default |
|----------|-------------|---------|
| `POSTGRES_PASSWORD` | PostgreSQL password | — |
| `Jwt__SecretKey` | JWT signing key (min 32 chars) | — |
| `Seed__AdminPassword` | Initial admin account password | — |
| `Seed__AdminUsername` | Initial admin account username | `admin` |

### Run Backend

```
cd ProjectWarehouse.Server
dotnet run
```

API: `https://localhost:7095`  
Scalar UI (dev only): `https://localhost:7095/scalar`

### Run Frontend

```
cd projectwarehouse.client
npm install
npm run dev
```

Dev server: `http://localhost:5173`  
Vite proxies `/api/*`, `/openapi/*`, `/scalar/*` → `https://localhost:7095`.

### Run Telemetry (optional)

```
docker compose --profile telemetry up -d
```

Raises the OTLP collector (`4317`/`4318`) and the Aspire Dashboard on `http://localhost:18888`; the backend
started by `dotnet run` exports traces and logs into it. The profile keeps both containers out of a plain
`docker compose up`. To run without them, set `Observability__OtlpEndpoint=none` — that switches the export
off instead of leaving it to time out. See
[observability-specification.md](observability-specification.md).

### First Login

Use the credentials set in `Seed:AdminPassword`. The admin user is seeded on startup with all permissions.
