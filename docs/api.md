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
- **Read endpoints that POST**: `POST /api/statistics/stock-movements/pivot` is the one read that takes a body.
  Its `metrics` are objects, each an action set plus a direction set plus a receipt-tag set, and a list of those
  does not survive a query string in any form worth parsing. The rest of the movement filter travels in the same
  body rather than being split across body and query.
- **Day boundaries**: where a timestamp is cut into a calendar day (`/api/events`,
  `/api/statistics/stock-movements/*`, `/api/stock-forecast`), the zone is never a query parameter. It is
  resolved server-side: the warehouse's own `TimeZoneId` when the request is narrowed to one warehouse,
  otherwise the caller's `X-Time-Zone` header (an IANA id, set for every request by the client interceptor),
  otherwise the server's zone. Responses echo the zone that was applied. See
  [stock-forecast-specification.md](stock-forecast-specification.md#резолвер-пояса).

## Authorization

Endpoints requiring authentication carry `[Authorize]` and expect `Authorization: Bearer <accessToken>`.
Most additionally require a permission string present in the JWT claims — see
[permissions.md](permissions.md) for the permission model and the `*_assigned` convention that narrows an
operation to the caller's assigned warehouses.

## JWT authentication

Access tokens are short-lived (15 min by default). Refresh tokens are long-lived (7 days), stored in the
database as a hash, and rotated on every use: `/api/auth/refresh` revokes the presented token and issues a new
pair. `/api/auth/logout` revokes without reissuing, and only a token belonging to the caller.

### Login protection

Guessing is bounded by a rate limit, not by a per-account lockout. `/api/auth/login` carries the `login`
policy: a fixed window of 30 requests per minute per client address, answering **429 `tooManyRequests`**. The
partition is the remote address, so a site behind one NAT shares a window — the limit is sized for a floor of
people mistyping a password, not for one browser. `/api/auth/refresh` is not rate limited; it is already gated
by a 512-bit opaque token.

**There is deliberately no account lockout.** Locking an account on failed attempts hands anyone who knows a
username a way to keep that account shut, and a locked response is itself an answer to "does this login
exist". Both are worse than the guessing the lockout would prevent, which the rate limit already bounds.

Login is therefore a plain 401 `invalidCredentials` for both an unknown username and a wrong password. Neither
path writes anything, and the unknown-username path verifies against a throwaway hash so the two spend the
same PBKDF2 work — they are indistinguishable by response body and by timing.

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

A user that no longer exists resolves to `SecurityVersionStore.NoSuchUser` (`-1`), a value no token can carry,
so deleting a user invalidates their outstanding access tokens immediately rather than at expiry. The store
also carries a generation counter: a read that began before an invalidation does not publish the value it
fetched, so a bump landing mid-read cannot be cached away.

**A failed token produces a bare 401 — there is no `AppProblemDetails` body and no `tokenOutdated` code on the
wire.** `ErrorCode.TokenOutdated` exists in the enum and has a client-side message, but nothing emits it; do not
write a client that branches on it.

**Client behaviour** is therefore code-blind: `apiClient.ts` refreshes and replays the request once on *any* 401,
and clears the session only when the refresh is *explicitly rejected* (4xx). That covers an expired token and an
outdated `security_version` with one path, which is why distinguishing them was never needed. A refresh that
fails because the server is unreachable or answers 5xx leaves the tokens in place — see
[frontend.md](frontend.md) for the three-state outcome the client branches on.

**Restart behaviour**: the store starts empty; the first request per user loads the version from the DB once,
then serves from memory for the lifetime of the process. Existing tokens survive a restart.

> ⚠️ **Single-instance only.** The in-memory dictionary is not shared across processes. Horizontal scaling
> requires replacing `SecurityVersionStore` with a distributed cache (Redis or similar), otherwise a bump on
> one instance leaves the others accepting the old token.

### Refresh token lifecycle

The token handed to the client is 64 bytes of CSPRNG output, base64url-encoded. What the `RefreshTokens` table
holds is `TokenHash` — the hex-encoded SHA-256 of that string, under a unique index. At 512 bits of entropy a
salt buys nothing, and a database dump yields no usable session. The row also carries `ExpiresAt` and
`RevokedAt`; `IsActive = !IsRevoked && !IsExpired`.

Refresh sets `RevokedAt = now` on the old row and inserts a new one; the revocation and the `IsActive` test are
one `ExecuteUpdateAsync` with the filter inlined, so two callers racing with the same token cannot both win.
Logout sets `RevokedAt = now`, matching on `UserId` as well as the hash.

**Reuse detection.** A refresh call that matches no active row is checked against the hash regardless of
state: if that row exists and was revoked more than `ReuseGracePeriod` (10s, not configurable) ago, the
presented token was valid once and got used or revoked well before this request — a legitimate client does
not replay its own refresh token that late, so this can only mean the token leaked and something else
consumed it first. The response stays the same generic `refreshTokenInvalid` either way, but every other
active refresh token for that `UserId` is revoked and `SecurityVersion` is bumped, ending every session the
account holds, not just this request's. A row that is merely expired, a hash that matches no row at all, or
one revoked within the grace period, does not trigger this — the grace period absorbs an ordinary race
between two requests rotating the same token at once (no `navigator.locks` in the tab, or a network retry),
where the loser seeing "already revoked" is expected, not a sign of theft.

Rotation leaves a dead row behind every access-token lifetime, so `RefreshTokensGcJob` (Quartz, cron
`Jwt:RefreshTokenGcCron`) deletes rows past `ExpiresAt` by more than `Jwt:RefreshTokenRetentionDays` (30 by
default). Expiry alone decides — a revoked row is unusable either way — and the retention window keeps recent
sessions readable, since these rows are the only trace a session leaves. Like the data-files GC it takes a
Postgres advisory lock, so two instances cannot collide.

### Configuration

```json
"Jwt": {
  "Issuer": "ProjectWarehouse",
  "Audience": "ProjectWarehouse",
  "AccessTokenExpirationMinutes": 15,
  "RefreshTokenExpirationDays": 7,
  "RefreshTokenRetentionDays": 30,
  "RefreshTokenGcCron": "0 45 3 * * ?",
  "RefreshTokenGcBatchSize": 5000,
  "SecretKey": "..."
}
```

`SecretKey` must come from an environment variable or user secrets outside development. The three
`RefreshTokenGc*` values drive `RefreshTokensGcJob`; the batch size bounds one `DELETE` statement, and the job
keeps issuing them until a batch comes back short.

`AspNetUsers` still carries Identity's `LockoutEnabled`, `LockoutEnd` and `AccessFailedCount` columns. Nothing
reads them — the counters are `SignInManager`'s, and login goes through `UserManager.CheckPasswordAsync`. Wiring
`SignInManager.PasswordSignInAsync` in anywhere would silently reintroduce the lockout described above.
