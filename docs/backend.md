# Administration backend

The .NET 10 application exposes REST and calls Moongate's `moongate.admin.v1` gRPC services using `Moongate.Admin.Contracts` 0.14.0. No direct database connection is required.

## Start locally

1. Install .NET SDK 10.0.401 and trust the ASP.NET Core development certificate with `dotnet dev-certs https --trust`.
2. Enable Moongate's administration listener and provision an API-enabled account using the upstream [administration guide](https://github.com/moongate-community/moongate/blob/develop/docs/admin-api.md).
3. For initial setup, inject the setup token from the designated Bitwarden item at runtime. Alternatively, retain an existing valid static endpoint catalog. Install the upstream public CA certificate in the backend's system trust store when using a private CA. Both trust and hostname must match.
4. Run `dotnet run --project src/Moongate.Admin.Api`. The Development profile listens at `https://localhost:7080`.
5. Build the UI using [frontend instructions](frontend.md) and open `https://localhost:7080/`, or use `https://localhost:7080/swagger` for the API. Complete initial setup and sign in. `/openapi/v1.json` describes the contract. Both documentation endpoints are Development-only.

Use `https://localhost:7080/health/live` for process liveness; it does not test Moongate connectivity. All `/api` requests require HTTPS. The backend rejects plaintext API requests with 400 `https_required`. Liveness and Development OpenAPI can be inspected without credentials.

## Endpoint configuration

A fresh checkout has an empty `Moongate` catalog and starts without contacting a remote server. Liveness, setup status and Development Swagger work immediately; login returns 503 `configuration_required` until configured.

An existing deployment may still supply this non-secret static `Moongate` section through appsettings or runtime environment configuration:

```json
{
  "Moongate": {
    "AuthenticationEndpointId": "login",
    "AllowInsecureLoopback": false,
    "Endpoints": [
      { "Id": "login", "Label": "Login", "Address": "https://127.0.0.1:2590" },
      { "Id": "game-1", "Label": "Game 1", "Address": "https://game-1.example.test:2590" }
    ]
  }
}
```

Replace the example Game address with your actual private endpoint. Endpoint IDs are unique and case-sensitive. Login and account operations always target `AuthenticationEndpointId`, which must point to a Login or Standalone server. Other configured endpoints can be Game servers for information reads. Normal server browsing selects configured IDs. Only protected configuration operations can submit or replace addresses.

Environment overrides use ASP.NET Core names, for example `Moongate__Endpoints__0__Address` and `Moongate__AuthenticationEndpointId`. Static settings are used only when no saved catalog exists. Saved configuration is authoritative for the complete catalog and endpoint arrays are never merged. Configure `AdminConfiguration__StoragePath` for the saved file; the default is `data/connections.json` relative to the content root. Persist its directory on a writable volume. Static changes require a restart and do not override an existing saved file. REST updates activate immediately.

For local gRPC development only, set `AllowInsecureLoopback` to true in Development and use an `http://127.0.0.1:<port>` or literal IPv6 loopback address. Moongate must also explicitly enable its loopback plaintext administration override. Hostnames and remote plaintext addresses are rejected. The REST API still requires HTTPS.

## Initial setup and connection management

Use the operator's designated setup item: 🔑 Bitwarden "<Moongate Admin setup token item>". Supply its random 32-byte base64url value (43 characters) through `MOONGATE_ADMIN_SETUP_TOKEN` at process start. Never put this token in appsettings, the saved catalog, an environment file or a command argument. A missing token leaves setup unavailable; a malformed configured value fails startup with a safe error.

1. GET `/api/configuration/status` to read `configured` and `setupAvailable`. This public response contains only those booleans.
2. In Swagger **Authorize**, enter the setup token in **SetupToken**. Alternatively send `X-Moongate-Setup-Token` explicitly. POST `/api/configuration/setup` with `authenticationEndpointId`, `allowInsecureLoopback` and `endpoints` using the catalog shape above. This saves the initial catalog and returns 201 with its ETag.
3. Clear the setup authorization and sign in through `/api/auth/login`. Setup closes immediately after saving and stays closed after restart. Existing valid static deployments already count as configured and cannot be taken over with a setup token.
4. An Administrator can GET `/api/configuration`, copy its ETag **including quotes** into `If-Match`, then PUT the complete replacement catalog to the same route. A successful response includes a new ETag and `reauthenticationRequired: true`; sign in again. Omitted endpoints are removed. Missing If-Match is 428; stale, weak, wildcard or multiple tags are 412 and cannot overwrite the active catalog.
5. Optionally POST `/api/configuration/test-connection` before saving. Send `configuration` containing the candidate catalog, temporary `username`/`password`, and optional `endpointId` (defaults to its authentication endpoint). Use the still-available setup token or an Administrator JWT. The probe signs in separately against the candidate authority, requires an API-enabled Administrator and Login/Standalone authentication mode, reads safe server information, then attempts temporary logout. It saves no settings or credentials and issues no REST JWT.

Configuration writes validate format without requiring remote availability, so connections can be provisioned before Moongate starts. A successful probe is a check at that moment, not a future availability guarantee. A Game endpoint can be tested using credentials for the candidate Login/Standalone authority in the same realm.

Catalogs contain 1–16 unique case-sensitive endpoint IDs of 1–64 ASCII letters, digits, dots, underscores or hyphens. Labels are nonblank, at most 100 UTF-16 code units and contain no control characters. Addresses have at most 2048 characters, require an absolute HTTPS root URL, and reject embedded credentials, paths, queries and fragments. Configuration request bodies have a 64 KiB limit, including chunked bodies. The Development-only literal-loopback exception above remains available; TLS trust/hostname checks and disabled redirects also apply to probes.

GET/PUT and JWT-authorized probes revalidate Administrator permission with the current upstream authority. Downgraded or revoked accounts cannot change configuration. Invalid credentials or an outage on a candidate do not clear the caller's valid administration session; correct the candidate request instead. A candidate logout outage is logged safely and does not overwrite successful information, with upstream expiration as the fallback.

Each request and local JWT session stays tied to one catalog revision. Updates reject old JWTs on subsequent requests. Already-started requests may finish against their original server; they never send their private token to a replacement address. A login completing during an update may return an old-revision JWT that requires another login.

Writes use a temporary file and atomic rename before publishing the new revision. Validation errors, stale ETags and failed writes leave the prior catalog intact. Saved documents include schema version 1 and a revision; corrupt, unreadable or unsupported documents fail startup rather than reopening setup. If the configured authority becomes unreachable and prevents normal Administrator revalidation, recover by editing the non-secret saved catalog offline and restarting. There is no REST reset/bypass endpoint. Keep one backend instance per catalog file.

## Sign in and call the API

Use the operator's designated vault item: 🔑 Bitwarden "<Moongate administration account item>". Supply credentials at runtime; never save them in appsettings, environment files, scripts, or logs.

1. POST `/api/auth/login` with JSON `username` and `password` over HTTPS.
2. Read the returned REST `accessToken`, `tokenType: Bearer`, safe account summary, and `expiresAt`.
3. Send `Authorization: Bearer <REST JWT>` with protected requests.
4. POST `/api/auth/logout` with that header to invalidate the current session. Logout without a valid local session is idempotent.
5. After expiry or restart, sign in again. A replacement login carrying the previous JWT invalidates its local session.

The REST JWT includes safe identity claims and a random session ID. It never contains Moongate's opaque gRPC token; that token stays in backend memory. The API validates JWT signature, algorithm, issuer, audience, absolute expiration, and local session existence. Protected upstream operations still validate Moongate's session, preserving revocation.

JWT Bearer requests use an explicit header; the API does not issue authentication cookies or use the previous CSRF endpoint. Keep frontend tokens in memory rather than persistent browser storage. Refresh tokens and cross-origin browser policies are not included in this release.

## Swagger UI

In Development, open `https://localhost:7080/swagger`. The embedded Swagger UI uses `/openapi/v1.json`; UI and assets are absent in Production.

For a fresh process, use the Configuration setup flow above first. Use **Try it out** on `/api/auth/login`, copy the returned REST `accessToken`, and enter it in **Authorize** as the token value, without adding another `Bearer` prefix. Subsequent protected operations use the header automatically. Authorization is not persisted across page reloads; the UI's external validator is disabled. Use disposable upstream environments when testing account mutations.

## REST operations

| Method and path | Success | Permission / behavior |
| --- | --- | --- |
| POST `/api/auth/login` | 200 | API-enabled account; safe identity and expiry |
| POST `/api/auth/logout` | 204 | Local logout is idempotent |
| GET `/api/auth/session` | 200 | Valid upstream session; safe identity and expiry |
| GET `/api/servers` | 200 | Valid upstream session; configured IDs and labels |
| GET `/api/servers/{id}` | 200 | Valid upstream session; selected server information |
| GET `/api/accounts` | 200 | Administrator; paginated accounts |
| POST `/api/accounts` | 201 | Administrator and Bearer JWT; create account |
| POST `/api/accounts/{id}/revoke-sessions` | 204 | Administrator and Bearer JWT; revoke administrative sessions |
| GET `/api/configuration/status` | 200 | Anonymous; setup/configured booleans only |
| POST `/api/configuration/setup` | 201 | Available setup token; first catalog only |
| GET `/api/configuration` | 200 | Upstream-revalidated Administrator; catalog and ETag |
| PUT `/api/configuration` | 200 | Upstream-revalidated Administrator and If-Match; requires new login |
| POST `/api/configuration/test-connection` | 200 | Available setup token or upstream-revalidated Administrator; candidate credentials |
| GET `/health/live` | 200 | Anonymous process liveness |

Account listing defaults to 50 items. `pageSize=0` selects that default; 200 is the maximum. Start with `afterAccountId=0`, then send the numeric `nextAfterAccountId`; zero means there are no more pages. Account IDs are unsigned 32-bit numbers. Server `uptimeSeconds` is a decimal string so JavaScript can preserve unsigned 64-bit values.

Create-account fields are `username`, `password`, optional `accountType` (`regular`, `gameMaster`, `administrator`), and `canAccessApi` (default false). Omitted or null role means Regular. Numeric and unknown roles are rejected. Usernames preserve case and whitespace, must be nonblank, and may contain at most 255 UTF-16 code units. Passwords are nonblank and may contain at most 1024 UTF-8 bytes. Neither field accepts NUL characters. A 201 response contains the safe created account summary and does not invent a resource-detail URL.

Account summaries expose account ID, username, role, API access/lock flags, and UTC creation time. They never expose passwords, hashes, email, or upstream tokens. Self-revocation immediately clears the local session. Other affected sessions fail on their next upstream-validated request.

## Errors and uncertain writes

Errors use `application/problem+json`, a stable `code`, and `correlationId`. The correlation ID also appears in safe operation logs. Upstream status mapping:

| gRPC status | HTTP |
| --- | --- |
| InvalidArgument | 400 |
| Unauthenticated | 401; clear local session |
| PermissionDenied | 403 |
| NotFound | 404 |
| AlreadyExists | 409 |
| ResourceExhausted | 429 |
| Unimplemented | 501 |
| Internal / Unknown | 502 |
| Unavailable / upstream Cancelled / TLS connection failure | 503 |
| DeadlineExceeded | 504 |

Malformed JSON, query binding, and validation failures return safe 400 responses. Application defects return redacted 500 responses. Caller cancellation aborts normally. Every RPC receives request cancellation and a 10-second deadline.

401 from the current authority requires sign-in again. A candidate probe can return 401 for its separate credentials while preserving the caller's local JWT. A dependency outage returns 503 and preserves the local session so a later request can recover. Logout is the exception: it always clears the local ticket after identifying a valid local JWT session, even when upstream revocation cannot be confirmed; such an error includes `localSessionCleared: true`.

Account creation has no automatic retry. A response lost after dispatch can follow a committed write; transport failures and malformed successful responses include `mutationOutcomeUnknown: true`. Use account listing to reconcile the username before deciding to create it again. A duplicate response is 409, not a reason to blindly retry. There is no exactly-once guarantee.

## Deployment and verification

This release supports one backend instance. Sessions and JWT signing keys are ephemeral; restart requires another sign-in. Do not load-balance it across instances or expect a JWT to work on another instance. Use a TLS-enabled Kestrel listener in production. Reverse proxies must use HTTPS to the backend as well. This application does not consume forwarded protocol headers.

Install only public CA material in the backend trust store. Keep server private keys and credentials in the designated secret store and provide them through the deployment's approved runtime mechanism. The backend never disables certificate validation and never needs Moongate's server private key.

Run repository verification:

```sh
dotnet restore MoongateAdmin.slnx --locked-mode
dotnet build MoongateAdmin.slnx -c Release --no-restore
dotnet test MoongateAdmin.slnx -c Release --no-build --logger trx
dotnet format MoongateAdmin.slnx --verify-no-changes --no-restore
```

The Swagger script smoke check also uses the Node.js runtime available in CI. Tests use an in-process Moongate gRPC fixture, including TLS trust/hostname failures, revoked and expired tokens, JWT signature/issuer/audience checks, account permissions, pagination, and creation response loss. They do not require PostgreSQL, Redis, or live operator credentials.

A live smoke test is optional and requires an enabled real endpoint plus runtime credentials from Bitwarden. Perform only login, server information, authorized listing, and logout. Do not automatically create or revoke live accounts during verification. Connection configuration is tracked in [issue #7](https://github.com/moongate-community/moongate-admin/issues/7). Initialization and backend development are tracked in [issue #1](https://github.com/moongate-community/moongate-admin/issues/1) and [issue #3](https://github.com/moongate-community/moongate-admin/issues/3).

## Frontend hosting

Run `scripts/build-frontend.sh` before `dotnet publish`. The host serves only the generated `wwwroot/frontend` application files, with an anonymous SPA entry for UI paths. Reserved `/api`, `/health`, `/swagger`, `/openapi`, missing assets and non-GET UI requests retain real 404 behavior; existing API HTTPS/authentication checks still apply. Without a built index the API remains available and UI paths return 404. Production exposes neither Swagger nor OpenAPI.
