# Project Warehouse — Documentation

A warehouse management web app with barcode/QR scanning (PWA), user management, and a role-based permission system.

## Architecture

```
projectwarehouse.client/   React 19 + TypeScript PWA (Vite)
ProjectWarehouse.Server/   ASP.NET Core 9 REST API
  ├── Controllers/         HTTP layer
  ├── Services/            Business logic
  ├── Domain/              EF Core entities
  ├── Infrastructure/      Auth handlers, permissions, error helpers
  ├── Models/              Request/response DTOs
  ├── Data/                DbContext, migrations, seeder
  └── Migrations/          EF Core migrations
```

**Backend:** ASP.NET Core 9, Entity Framework Core, PostgreSQL, ASP.NET Core Identity, JWT Bearer auth  
**Frontend:** React 19, TypeScript 6, Vite 8, MUI v9, React Router v7, PWA (vite-plugin-pwa), zxing-wasm

## Docs Index

| File | Contents |
|------|----------|
| [api.md](api.md) | REST API endpoints, request/response shapes, models |
| [auth.md](auth.md) | JWT auth flow, token refresh, SecurityVersion invalidation |
| [permissions.md](permissions.md) | Permission system design, available permissions, RBAC |
| [errors.md](errors.md) | Error response format (`AppProblemDetails`), all error codes |
| [validation.md](validation.md) | Validation pipeline, `[JsonRequired]`, ModelState mapping |
| [changelog.md](changelog.md) | Changelog system: architecture, how to add tracking to new methods, Action/ActionData |
| [backend-patterns.md](backend-patterns.md) | Recurring backend implementation patterns (search, etc.) |
| [frontend.md](frontend.md) | Frontend architecture, pages, components, routing |
| [technical-specification.md](technical-specification.md) | WMS operational flows — data models, APIs, UX requirements |
| [orders-specification.md](orders-specification.md) | Orders system — FBS, FBO, Direct, assembly tasks, status flows |

## Local Dev Setup

### Prerequisites

- .NET 9 SDK
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

### First Login

Use the credentials set in `Seed:AdminPassword`. The admin user is seeded on startup with all permissions.
