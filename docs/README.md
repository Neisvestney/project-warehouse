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
| [backend-patterns.md](backend-patterns.md) | Recurring backend patterns — controller logic in services, search, inheritable fields, list sync, background work, access rules, enums |
| [entity-audit-log.md](entity-audit-log.md) | Entity audit log — before/after diffs of every mutation, how to add tracking to a method, Action/ActionData |
| [observability-specification.md](observability-specification.md) | Telemetry — OpenTelemetry traces and logs, file archive on prod, local analysis stack |
| [backlog.md](backlog.md) | Deferred work — what is blocked, why, and the event that unblocks it |

**Frontend**

| File | Contents |
|------|----------|
| [frontend.md](frontend.md) | Architecture: tech stack, directory layering, routing, cross-cutting conventions, pages, providers, PWA, API client |
| [frontend-components.md](frontend-components.md) | Component reference grouped by domain — catalog, files, marketplace, orders, warehouse, forms |
| [frontend-state.md](frontend-state.md) | How state enters and leaves components — URL state hooks, overlays, form hooks, `ObservableForm` |
| [frontend-realtime.md](frontend-realtime.md) | `RealtimeProvider`, subscription hooks, presence, edit-lock and stale-data UI |
| [native-client.md](native-client.md) | Capacitor build — predefined servers, hardware scanner plugin, native caveats |

**Domain specifications**

| File | Contents |
|------|----------|
| [technical-specification.md](technical-specification.md) | WMS operational flows — receipts, transfers, write-offs, stocktakes |
| [orders-specification.md](orders-specification.md) | Orders — FBS, FBO supplies & postings, Direct, external orders, assembly tasks, status flows |
| [items-specification.md](items-specification.md) | Catalog items — types, `FullName`, inheritance, tags, images, listing rules |
| [stock-forecast-specification.md](stock-forecast-specification.md) | Прогноз остатков — расчёт «на сколько дней хватит», окно расхода, пороги предупреждения |
| [analytics-specification.md](analytics-specification.md) | Аналитика — сводка по каналам, выплаты маркетплейсов, ABC/XYZ-анализ |
| [marketplaces-specification.md](marketplaces-specification.md) | Integration platform — Ozon Seller API, client codegen, credential storage, warehouse/card sync & mapping |
| [marketplaces-orders-specification.md](marketplaces-orders-specification.md) | Order sync — FBS & FBO posting discovery, status catch-up, history backfill, order creation, label retrieval |
| [marketplaces-returns-specification.md](marketplaces-returns-specification.md) | Returns sync — Ozon returns list, return kinds vs cancellations, compensation pass, link to orders |
| [realtime-specification.md](realtime-specification.md) | Real-time transport — SSE, event schema, watch registry, advisory edit locks |
| [data-files-specification.md](data-files-specification.md) | File storage — upload, storage abstraction, FK attachments, orphan GC, serving rules |
| [assembler-daily-routine.md](assembler-daily-routine.md) | Инструкция сборщика — рабочий день целиком: Ozon, волны отсечек, листик, вечерняя сборка и отгрузка |

## Recipes

Before adding a new piece of a known kind, open its recipe — the project already has a hook, component or
pattern for it, and a neighbouring file is not a reliable template. When no row matches, read the doc file for
that layer (the Docs Index above) before writing code.

**Frontend**

| Adding… | Recipe |
|---------|--------|
| A new component, hook or helper — where it goes | [frontend.md → Directory layering](frontend.md#directory-layering) |
| A modal, drawer or any overlay | [frontend-state.md → Overlays](frontend-state.md#overlays) — `useBackClosable` / `useDrawerSearchParamsState`, `useRetainedValue` + `onExited` |
| A confirmation dialog | [frontend-components.md → `ConfirmDialog`](frontend-components.md#confirmdialog) |
| A list table with pagination | [frontend-state.md → `usePaginatedParams`](frontend-state.md#usepaginatedparamsdebouncedparams-debounceddeps-immediateparams-immediatedeps-options), [frontend-components.md → `DataTableContainer`, `LinkTableRow`, `TableRowLoader`](frontend-components.md#layout--tables) |
| A table that must work on a phone | [frontend.md → Таблицы на узких экранах](frontend.md#таблицы-на-узких-экранах) |
| Status tabs with counts on a document list | [frontend-components.md → `StatusTabs`](frontend-components.md#statustabs), [backend-patterns.md → `PaginatedWithMeta`](backend-patterns.md#paginatedwithmetat-tmeta) |
| Filters or any state kept in the URL | [frontend-state.md → URL State Hooks](frontend-state.md#url-state-hooks), [frontend-components.md → `FiltersBar`](frontend-components.md#filtersbar) |
| A detail page with tabs | [frontend-state.md → Tabbed detail pages](frontend-state.md#tabbed-detail-pages) |
| Row selection and bulk actions | [frontend-state.md → `useSelectedItems`](frontend-state.md#useselecteditemsgetid-freshitems), [frontend-components.md → `BulkBar`](frontend-components.md#bulkbar) |
| A form | [frontend-state.md → Form Hooks](frontend-state.md#form-hooks), [frontend-components.md → Forms](frontend-components.md#forms) |
| A permission check in the UI | [frontend.md → Checking permissions](frontend.md#checking-permissions) |
| Query invalidation after a mutation | [frontend.md → Invalidating by operation](frontend.md#invalidating-by-operation) |
| A query error for a whole page | [frontend-components.md → `QueryErrorHandler`](frontend-components.md#queryerrorhandler) |
| An error shown inside a page section | [frontend.md → Inline error branches](frontend.md#inline-error-branches) |
| A loading overlay | [frontend-components.md → `LoadingOverlay`](frontend-components.md#loadingoverlay) |
| A date without time | [frontend.md → Date-only values](frontend.md#date-only-values) |
| A count with a Russian noun | [frontend.md → `pluralUtils`](frontend.md#pluralutils) |
| A file download | [frontend.md → Downloading a generated file](frontend.md#downloading-a-generated-file) |
| A copyable value, or text that acts on click | [frontend-components.md → `HoverActionLink`](frontend-components.md#hoveractionlink), [`CopyableText`](frontend-components.md#copyabletext) |
| A chip color | [frontend.md → Chip colors](frontend.md#chip-colors) |
| Memoization, MobX or `watch()` in a component | [frontend.md → React Compiler](frontend.md#react-compiler) |
| Live updates, presence or an edit lock | [frontend-realtime.md](frontend-realtime.md) |

**Backend**

| Adding… | Recipe |
|---------|--------|
| A controller action, or logic several actions share | [backend-patterns.md → Shared controller logic lives in a service](backend-patterns.md#shared-controller-logic-lives-in-a-service) |
| Request validation | [validation.md → How Validation Works](validation.md#how-validation-works) |
| An error returned from a controller | [errors.md → Controller Helpers](errors.md#controller-helpers) |
| A new error code | [errors.md → Where the codes are documented](errors.md#where-the-codes-are-documented) |
| Text search over a list | [backend-patterns.md → Search](backend-patterns.md#search-with-wherematchessearch--projectable) |
| A field inherited from a parent entity | [backend-patterns.md → Inheritable fields](backend-patterns.md#inheritable-fields-with-projectable) |
| Saving a nested list from a request | [backend-patterns.md → `IListUpdater`](backend-patterns.md#updating-related-entity-lists-with-ilistupdater) |
| A query loading several collections | [backend-patterns.md → Splitting mode](backend-patterns.md#a-query-loading-more-than-one-collection-picks-its-splitting-mode) |
| A paginated list with extra totals | [backend-patterns.md → `PaginatedWithMeta`](backend-patterns.md#paginatedwithmetat-tmeta) |
| Several aggregates over one table | [backend-patterns.md → `Concat` into `UNION ALL`](backend-patterns.md#many-aggregates-over-one-table-concat-into-a-single-union-all) |
| An invariant spanning a whole table | [backend-patterns.md → `pg_advisory_xact_lock`](backend-patterns.md#table-wide-invariants-pg_advisory_xact_lock-not-a-retry) |
| A counter row | [backend-patterns.md → Counter rows](backend-patterns.md#counter-rows-unique-index--xmin--replay) |
| A background job | [backend-patterns.md → Background work](backend-patterns.md#background-work-queue--worker--advisory-lock) |
| A file attachment point | [backend-patterns.md → File attachments](backend-patterns.md#file-attachments-adding-a-new-attachment-point) |
| An enum | [backend-patterns.md → Enums](backend-patterns.md#enums-pinned-values-free-ordering) |
| An access rule for an entity | [backend-patterns.md → Access rules](backend-patterns.md#access-rules-one-predicate-per-entity-type), [permissions.md → Adding a rule for a new entity](permissions.md#adding-a-rule-for-a-new-entity) |
| A permission | [permissions.md → Adding a New Permission](permissions.md#adding-a-new-permission) |
| Audit tracking for a mutation | [entity-audit-log.md → Adding Changelog to a New Method](entity-audit-log.md#adding-changelog-to-a-new-method) |

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

Raises the OTLP collector (`4317` for the backend over gRPC, `4318` for the frontend's OTLP/HTTP proxied
through `/api/telemetry`) and the Aspire Dashboard on `http://localhost:18888`; the backend started by
`dotnet run` exports traces and logs into it. The profile keeps both containers out of a plain
`docker compose up`. To run without them, set `Observability__OtlpEndpoint=none` and
`Observability__OtlpHttpEndpoint=none` — that switches the export off instead of leaving it to time out. See
[observability-specification.md](observability-specification.md).

### First Login

Use the credentials set in `Seed:AdminPassword`. The admin user is seeded on startup with all permissions.

## Licence note

Image resizing uses **SixLabors.ImageSharp 3.x**, under the Six Labors Split License: free for organizations
under $1M annual revenue, commercial licence required above it. If that threshold is crossed, the alternative is
SkiaSharp (MIT), which needs native Linux assets in the image.
