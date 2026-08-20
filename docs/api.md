# API Conventions & Authentication

There is no hand-written endpoint reference. The endpoint list, request/response shapes and enum values are
generated from the code and would only rot if mirrored here. Read them from:

| Source | What it gives |
|--------|---------------|
| `ProjectWarehouse.Server/Controllers/*.cs` | Routes, `[Authorize]`, required permissions, error codes — in the XML `<summary>` / `<remarks>` above each action |
| `projectwarehouse.client/src/api/types.gen.ts` | Every DTO and enum, generated from OpenAPI and committed |
| `projectwarehouse.client/src/api/sdk.gen.ts` | The typed client — one function per endpoint |
| `https://localhost:7095/scalar` | Browsable OpenAPI UI (dev only, server must be running) |

Behavioural rules that are *not* readable off a signature live in the domain specs — see the index in
[README.md](README.md). This file holds only what is true of every endpoint.

## Transport

Base URL: `https://localhost:7095` (dev) / configured host (prod).

Requests and responses are `application/json`, with three exceptions: `/api/files` upload takes
`multipart/form-data` and its content endpoints return raw byte streams, `POST /api/orders/labels` returns
`application/pdf`, and `/api/realtime/stream` returns `text/event-stream`.

Errors always use `AppProblemDetails` — see [errors.md](errors.md) for the envelope and the full code list.

## Common query conventions

- **Pagination**: `page` (default 1), `pageSize` (default 20, max 200) → `Paginated<T>`.
- **Search**: `searchString` matches against the entity's precomputed `SearchString` column.
- **Sorting**: `sortBy` (per-endpoint enum) plus `sortOrder` (`asc` | `desc`).
- **Multi-value filters**: repeatable params (`itemTypes`, `tagIds`, `catalogItemTypes`) use OR semantics.
- **`utcOffsetMinutes`**: the caller's offset in minutes, used where a timestamp must be cut into a calendar
  day the way the user sees it (`/api/events`, `/api/statistics/stock-movements/*`).

## Authorization

Endpoints requiring authentication carry `[Authorize]` and expect `Authorization: Bearer <accessToken>`.
Most additionally require a permission string present in the JWT claims — see
[permissions.md](permissions.md) for the permission model and the `*_assigned` convention that narrows an
operation to the caller's assigned warehouses.

## JWT authentication

Access tokens are short-lived (15 min by default). Refresh tokens are long-lived (7 days), stored in the
database, and rotated on every use: `/api/auth/refresh` revokes the presented token and issues a new pair.
`/api/auth/logout` revokes without reissuing.

### Access token claims

| Claim | Value |
|-------|-------|
| `sub` | User ID (Guid) |
| `name` | Username |
| `email` | Email (optional) |
| `given_name` | First name (optional) |
| `family_name` | Last name (optional) |
| `security_version` | Integer version counter |
| `permission` | One claim per permission (`"users.view"`, etc.) |

Permissions are baked into the token, which is why changing them has to invalidate it.

### SecurityVersion — token invalidation

`ApplicationUser.SecurityVersion` is an integer counter stored in the database and cached in
`SecurityVersionStore` (a singleton in-memory `ConcurrentDictionary<Guid, int>`).

On every authenticated request `JwtBearerEvents.OnTokenValidated` compares the token's `security_version`
claim against the store and calls `ctx.Fail("TOKEN_OUTDATED")` on a mismatch.

The counter is bumped when a role's permissions change (all users holding that role), when a user's direct
permissions change, and when a user's role assignment changes.

**A failed token produces a bare 401 — there is no `AppProblemDetails` body and no `tokenOutdated` code on the
wire.** `ErrorCode.TokenOutdated` exists in the enum and has a client-side message, but nothing emits it; do not
write a client that branches on it.

**Client behaviour** is therefore code-blind: `apiClient.ts` refreshes and replays the request once on *any* 401,
and only clears the session when the refresh itself fails. That covers an expired token and an outdated
`security_version` with one path, which is why distinguishing them was never needed.

**Restart behaviour**: the store starts empty; the first request per user loads the version from the DB once,
then serves from memory for the lifetime of the process. Existing tokens survive a restart.

> ⚠️ **Single-instance only.** The in-memory dictionary is not shared across processes. Horizontal scaling
> requires replacing `SecurityVersionStore` with a distributed cache (Redis or similar), otherwise a bump on
> one instance leaves the others accepting the old token.

### Refresh token lifecycle

Stored in the `RefreshTokens` table with `ExpiresAt` and `RevokedAt`; `IsActive = !IsRevoked && !IsExpired`.
Refresh sets `RevokedAt = now` on the old row and inserts a new one; logout sets `RevokedAt = now`.

### Configuration

```json
"Jwt": {
  "Issuer": "ProjectWarehouse",
  "Audience": "ProjectWarehouse",
  "AccessTokenExpirationMinutes": 15,
  "RefreshTokenExpirationDays": 7,
  "SecretKey": "..."
}
```

`SecretKey` must come from an environment variable or user secrets outside development.
