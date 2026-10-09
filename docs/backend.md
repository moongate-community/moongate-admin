# Administration backend

The .NET 10 application exposes REST and calls Moongate's `moongate.admin.v1` gRPC services through `Moongate.Admin.Contracts` 0.14.0. It needs no database connection.

## Start locally

1. Install .NET SDK 10.0.401 and trust the development certificate with `dotnet dev-certs https --trust`.
2. Enable Moongate's administration listener and provision an API-enabled account (see the upstream administration guide). For a private CA, install its public certificate in the backend's trust store; both trust and hostname must match.
3. Configure the endpoints below, then run `dotnet run --project src/Moongate.Admin.Api`. The Development profile listens at `https://localhost:7080`.
4. Open `https://localhost:7080/swagger`. `/openapi/v1.json` describes the contract. Both are Development-only.

`/health/live` reports process liveness only. All `/api` requests require HTTPS; plaintext requests get 400 `https_required`.

## Configuration

Server addresses are non-secret and come from the `Moongate` section (appsettings or environment, e.g. `Moongate__Endpoints__0__Address`):

```json
{
  "Moongate": {
    "AuthenticationEndpointId": "login",
    "AllowInsecureLoopback": false,
    "Endpoints": [
      { "Id": "login", "Label": "Login", "Address": "https://login.example.test:2590" },
      { "Id": "game-1", "Label": "Game 1", "Address": "https://game-1.example.test:2590" }
    ]
  }
}
```

Rules, checked at startup (invalid configuration fails startup):

- 1-16 endpoints. IDs are unique, case-sensitive, 1-64 characters from ASCII letters, digits, `.`, `_`, `-`.
- Labels are nonblank, at most 100 characters, without control characters.
- Addresses are absolute HTTPS root URLs of at most 2048 characters, without credentials, path, query or fragment.
- `AuthenticationEndpointId` must name a configured Login or Standalone server. Login and all account operations use it; other endpoints may be Game servers, readable through the server routes.
- `AllowInsecureLoopback` permits `http://` only in Development and only for literal loopback IPs. Moongate must enable its own loopback override too.

An empty section is valid: the process starts without contacting any server, and login returns 503 `configuration_required`. Configuration changes need a restart.

## Serving the frontend

When `Frontend:Path` (default `../../frontend/dist`, relative to the content root, which is the API project when running from the repo; use an absolute path in deployments) contains an `index.html`, the host serves the built frontend from the same origin. Existing files are served directly, `index.html` is sent `no-store`, and files under `/assets` are cached as immutable. Unmatched GET requests without a file extension fall back to `index.html` so deep links work. `/api`, `/health`, `/swagger` and `/openapi` never fall back, and non-GET requests and missing files with an extension return 404. If the directory has no `index.html`, the host behaves exactly as without a frontend.

## Sign in and call the API

1. `POST /api/auth/login` with JSON `username` and `password` over HTTPS. Take credentials from your secret store at runtime; never put them in files, scripts or logs.
2. Read `accessToken` (a REST JWT), `tokenType: Bearer`, the account summary and `expiresAt`.
3. Send `Authorization: Bearer <accessToken>` on protected requests.
4. `POST /api/auth/logout` ends the session. Without a valid local session it does nothing and returns 204.
5. After expiry or a backend restart, sign in again. A login that carries the previous JWT replaces and revokes the previous session.

The JWT carries `sub`, `name`, `role` and a random session ID. Moongate's opaque gRPC token never leaves backend memory. Each protected call also validates the session upstream, so revocation applies immediately. In Swagger, paste the `accessToken` into **Authorize** without a `Bearer` prefix.

## REST operations

| Method and path | Success | Permission / behavior |
| --- | --- | --- |
| POST `/api/auth/login` | 200 | Anonymous; safe identity and expiry |
| POST `/api/auth/logout` | 204 | Idempotent; local session always cleared |
| GET `/api/auth/session` | 200 | Valid session, revalidated upstream |
| GET `/api/servers` | 200 | Valid session; IDs and labels only |
| GET `/api/servers/{id}` | 200 | Valid session; unknown ID is 404 |
| GET `/api/accounts` | 200 | Administrator; cursor pagination |
| POST `/api/accounts` | 201 | Administrator; no `Location` header |
| POST `/api/accounts/{id}/revoke-sessions` | 204 | Administrator; `id` nonzero |
| GET `/health/live` | 200 | Anonymous |

Account listing: `pageSize=0` (or omitted) selects 50, the maximum is 200. Start with `afterAccountId=0` and send the returned `nextAfterAccountId` until it is 0. Account IDs are unsigned 32-bit. Server `uptimeSeconds` is a decimal string so unsigned 64-bit values survive JavaScript.

Create-account fields: `username`, `password`, optional `accountType` (`regular`, `gameMaster`, `administrator`; omitted or null means regular; numbers and unknown values are rejected) and `canAccessApi` (default false). Usernames keep case and whitespace, are nonblank, at most 255 UTF-16 code units, no NUL. Passwords are nonblank, at most 1024 UTF-8 bytes, no NUL. Summaries expose ID, username, role, API/lock flags and UTC creation time, never passwords, hashes, email or tokens.

The `administrator` role in the JWT is a first filter; Moongate stays authoritative, so a downgraded account gets 403 from upstream. Revoking your own account's sessions also clears the local session.

## Errors

Errors are `application/problem+json` with a stable `code` and a `correlationId` that also appears in safe logs.

| gRPC status | HTTP |
| --- | --- |
| InvalidArgument | 400 |
| Unauthenticated | 401, local session cleared |
| PermissionDenied | 403 |
| NotFound | 404 |
| AlreadyExists | 409 |
| ResourceExhausted | 429 |
| Unimplemented | 501 |
| Internal / Unknown | 502 |
| Unavailable / Cancelled / TLS failure | 503 |
| DeadlineExceeded | 504 |

Codes are `upstream_<status>` for upstream failures, plus `https_required`, `authentication_required`, `permission_denied`, `configuration_required`, `bad_request` and `request_failed` (500, redacted). Malformed JSON, binding and validation failures are 400. Every RPC has request cancellation and a 10-second deadline.

A 503 keeps the local session so a later request can recover. Logout is the exception: it clears the local session even when upstream revocation fails, and that error carries `localSessionCleared: true`.

Account creation is never retried. If its outcome is uncertain (lost response, transport failure or a malformed success), the error carries `mutationOutcomeUnknown: true`: list accounts to reconcile the username before creating it again. A 409 is a duplicate, not a reason to retry blindly. There is no exactly-once guarantee.

## Deployment and verification

Run one backend instance. Sessions and the JWT signing key are in memory, so a restart requires signing in again and a JWT is not valid on another instance. Use a TLS-enabled Kestrel listener in production; a reverse proxy must also use HTTPS to the backend, because forwarded protocol headers are not consumed. Install only public CA material in the trust store. The backend never disables certificate validation.

```sh
dotnet restore MoongateAdmin.slnx --locked-mode
dotnet build MoongateAdmin.slnx -c Release --no-restore
dotnet test MoongateAdmin.slnx -c Release --no-build --logger trx
dotnet format MoongateAdmin.slnx --verify-no-changes --no-restore
```

Tests use an in-process fake Moongate gRPC server, including TLS trust and hostname failures; they need no PostgreSQL, Redis or real credentials. A live smoke test is optional: sign in, read server information, list accounts and sign out, using credentials from the secret store. Do not create or revoke live accounts automatically.
