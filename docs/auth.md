# Authentication & JWT

## Overview

The app uses JWT Bearer authentication. Access tokens are short-lived (15 min by default). Refresh tokens are long-lived (7 days) and stored in the database with rotation on use.

## Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/login` | — | Get token pair |
| POST | `/api/auth/refresh` | — | Rotate refresh token |
| POST | `/api/auth/logout` | Bearer | Revoke refresh token |
| PUT | `/api/auth/password` | Bearer | Change own password (requires current password) |
| GET | `/api/auth/me` | Bearer | Current user info + permissions |

### Login

```http
POST /api/auth/login
Content-Type: application/json

{ "username": "admin", "password": "Admin1234!" }
```

Response:
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "base64string",
  "expiresIn": 900
}
```

### Refresh

```http
POST /api/auth/refresh
Content-Type: application/json

{ "refreshToken": "base64string" }
```

Returns a new token pair. The old refresh token is revoked immediately (rotation).

### Using the token

```http
GET /api/auth/me
Authorization: Bearer eyJ...
```

## Access Token Claims

| Claim | Value |
|-------|-------|
| `sub` | User ID (Guid) |
| `name` | Username |
| `email` | Email (optional) |
| `given_name` | First name (optional) |
| `family_name` | Last name (optional) |
| `security_version` | Integer version counter |
| `permission` | One claim per permission (`"users.view"`, etc.) |

## SecurityVersion — Token Invalidation

`ApplicationUser.SecurityVersion` is an integer counter stored in the database and cached in `SecurityVersionStore` (singleton in-memory `ConcurrentDictionary<Guid, int>`).

**On every authenticated request**, `JwtBearerEvents.OnTokenValidated` checks:
1. Read `security_version` from the JWT claim
2. Read current version from `SecurityVersionStore` (in-memory, DB-backed on first access after restart)
3. If mismatch → fail with `TOKEN_OUTDATED`

**When is `SecurityVersion` bumped?**
- Role permissions change → all users with that role get bumped
- User's direct permissions change → that user gets bumped
- User's role assignment changes → that user gets bumped

**Client behavior on `TOKEN_OUTDATED` (401)**:
1. Detect the error (check `errors.root[0].code == "tokenOutdated"`)
2. Call `POST /api/auth/refresh` with the current refresh token
3. New access token will have the updated `security_version` — valid again

**Restart behavior**: `SecurityVersionStore` starts empty. First request per user hits the DB once to load the version, then stays in memory for the lifetime of the process. Existing tokens remain valid after restart (version is restored from DB).

> ⚠️ **Single-instance only**: the in-memory `ConcurrentDictionary` is not shared across processes. For horizontal scaling (multiple app instances), replace `SecurityVersionStore` with a distributed cache (Redis, etc.) so all instances see the same version after a bump.

## Refresh Token Lifecycle

- Stored in `RefreshTokens` table with `ExpiresAt`, `RevokedAt`
- `IsActive = !IsRevoked && !IsExpired`
- On `/refresh`: old token gets `RevokedAt = now`, new token is created
- On `/logout`: token gets `RevokedAt = now`

## Configuration

```json
"Jwt": {
  "Issuer": "ProjectWarehouse",
  "Audience": "ProjectWarehouse",
  "AccessTokenExpirationMinutes": 15,
  "RefreshTokenExpirationDays": 7,
  "SecretKey": "..."   ← development only, use env var / secrets in prod
}
```
