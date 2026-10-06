# Administration backend

The .NET 10 application exposes REST and calls Moongate's `moongate.admin.v1` gRPC services using `Moongate.Admin.Contracts` 0.14.0. No direct database connection is required.

## Start locally

1. Install .NET SDK 10.0.401 and trust the ASP.NET Core development certificate with `dotnet dev-certs https --trust`.
2. Enable Moongate's administration listener and provision an API-enabled account using the upstream [administration guide](https://github.com/moongate-community/moongate/blob/develop/docs/admin-api.md).
3. Configure the endpoint set below. Install the upstream public CA certificate in the backend's system trust store when using a private CA. Both trust and hostname must match.
4. Run `dotnet run --project src/Moongate.Admin.Api`. The Development profile listens at `https://localhost:7080`.
5. Read `https://localhost:7080/openapi/v1.json` for the REST contract. Production does not expose this document.

Use `https://localhost:7080/health/live` for process liveness; it does not test Moongate connectivity. All `/api` requests require HTTPS. The backend rejects plaintext API requests with 400 `https_required`. Liveness and Development OpenAPI can be inspected without credentials.

## Endpoint configuration

The `Moongate` section in `src/Moongate.Admin.Api/appsettings.json` contains non-secret configuration:

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

Replace the example Game address with your actual private endpoint. Endpoint IDs are unique and case-sensitive. Login and account operations always target `AuthenticationEndpointId`, which must point to a Login or Standalone server. Other configured endpoints can be Game servers for information reads. Clients select configured IDs; they cannot submit addresses or change endpoint configuration through REST.

Environment overrides use ASP.NET Core names, for example `Moongate__Endpoints__0__Address` and `Moongate__AuthenticationEndpointId`. Configuration validates at startup; it does not contact upstream until an operation is requested. Restart the backend after changing endpoints.

For local gRPC development only, set `AllowInsecureLoopback` to true in Development and use an `http://127.0.0.1:<port>` or literal IPv6 loopback address. Moongate must also explicitly enable its loopback plaintext administration override. Hostnames and remote plaintext addresses are rejected. The REST API still requires HTTPS.

## Sign in and call the API

Use the operator's designated vault item: 🔑 Bitwarden "<Moongate administration account item>". Supply the username and password at runtime; never store them in appsettings, `.env`, scripts, request examples, or logs. Obtain credentials in the calling client from the vault; the backend does not require a stored administrator password.

1. GET `/api/auth/csrf`, retaining its cookie and returned `requestToken`.
2. POST `/api/auth/login` with JSON username/password and the `X-CSRF-TOKEN` header.
3. Retain the opaque `__Host-MoongateAdmin` cookie. Fetch a fresh CSRF request token after login.
4. Call protected GET routes with the cookie. Send `X-CSRF-TOKEN` on every POST, including logout.
5. After logout, fetch a fresh CSRF token before another login. A replacement login issues a distinct session reference and invalidates the previous local cookie.

Cookies are HttpOnly, Secure, SameSite Strict, and have no domain scope. Keep the browser and REST API on the same origin; no cross-origin credentials policy is enabled. Future frontend work will use this contract. There are no frontend build commands in this release.

## REST operations

| Method and path | Success | Permission / behavior |
| --- | --- | --- |
| GET `/api/auth/csrf` | 200 | Anonymous; issue CSRF request token |
| POST `/api/auth/login` | 200 | API-enabled account; safe identity and expiry |
| POST `/api/auth/logout` | 204 | Valid CSRF; local logout is idempotent |
| GET `/api/auth/session` | 200 | Valid upstream session; safe identity and expiry |
| GET `/api/servers` | 200 | Valid upstream session; configured IDs and labels |
| GET `/api/servers/{id}` | 200 | Valid upstream session; selected server information |
| GET `/api/accounts` | 200 | Administrator; paginated accounts |
| POST `/api/accounts` | 201 | Administrator and CSRF; create account |
| POST `/api/accounts/{id}/revoke-sessions` | 204 | Administrator and CSRF; revoke administrative sessions |
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

Malformed JSON, query binding, validation, and CSRF failures return safe 400 responses. Application defects return redacted 500 responses. Caller cancellation aborts normally. Every RPC receives request cancellation and a 10-second deadline.

401 requires sign-in again. A dependency outage returns 503 and preserves the local session so a later request can recover. Logout is the exception: it always clears the local ticket after valid CSRF, even when upstream revocation cannot be confirmed; such an error includes `localSessionCleared: true`.

Account creation has no automatic retry. A response lost after dispatch can follow a committed write; transport failures include `mutationOutcomeUnknown: true`. Use account listing to reconcile the username before deciding to create it again. A duplicate response is 409, not a reason to blindly retry. There is no exactly-once guarantee.

## Deployment and verification

This release supports one backend instance. Sessions and Data Protection keys are ephemeral; restart requires another sign-in. Do not load-balance it across instances or expect a cookie to work on another instance. Use a TLS-enabled Kestrel listener in production. Reverse proxies must use HTTPS to the backend as well. This application does not consume forwarded protocol headers.

Install only public CA material in the backend trust store. Keep server private keys and credentials in the designated secret store and provide them through the deployment's approved runtime mechanism. The backend never disables certificate validation and never needs Moongate's server private key.

Run repository verification:

```sh
dotnet restore MoongateAdmin.slnx --locked-mode
dotnet build MoongateAdmin.slnx -c Release --no-restore
dotnet test MoongateAdmin.slnx -c Release --no-build --logger trx
dotnet format MoongateAdmin.slnx --verify-no-changes --no-restore
```

Tests use an in-process Moongate gRPC fixture, including TLS trust/hostname failures, revoked and expired tokens, CSRF transitions, account permissions, pagination, and creation response loss. They do not require PostgreSQL, Redis, or live operator credentials.

A live smoke test is optional and requires an enabled real endpoint plus runtime credentials from Bitwarden. Perform only login, server information, authorized listing, and logout. Do not automatically create or revoke live accounts during verification. Initialization and backend development are tracked in [issue #1](https://github.com/moongate-community/moongate-admin/issues/1) and [issue #3](https://github.com/moongate-community/moongate-admin/issues/3).
