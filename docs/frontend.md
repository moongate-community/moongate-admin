# Administration frontend

The English-language UI uses Vite, React, TypeScript, Tailwind and shadcn/ui. The original Moongate assets and approved dark/light palette are shared throughout the application. Dark is the default; theme preference is the only browser-storage value.

## Development

1. Install Node 24 from `frontend/.nvmrc` and run `npm --prefix frontend ci`.
2. Start the backend with `dotnet run --project src/Moongate.Admin.Api`. Its Development HTTPS profile listens at `https://localhost:7080`.
3. Configure Node to trust the backend's public certificate or issuing CA, using `NODE_EXTRA_CA_CERTS=/absolute/path/to/public-ca.pem`. The hostname must match. Export only a public certificate; keep private keys and credentials in the designated secret store. This trust configuration must be set before starting Vite.
4. Run `npm --prefix frontend run dev` and open `http://127.0.0.1:5173`. Vite binds loopback and proxies `/api` to the HTTPS backend with verification enabled. `MOONGATE_ADMIN_BACKEND_URL` may select another HTTPS backend; it is a non-secret proxy setting, never a browser credential.

If setup is unavailable on a fresh backend, an operator must enable it by injecting the setup token from Bitwarden at runtime. Enter the key in the masked initial-setup form. Tests and builds never need a real setup key or live accounts.

## Operator workflows

Setup edits up to 16 connections and selects a Login or Standalone authentication server. Candidate tests use separate temporary credentials and do not save the catalog. The Development loopback override does not enable plaintext remote or Production connections.

Login keeps the REST JWT and account in memory. Reloading requires login again. Logout, expiry, self-revocation and saved configuration clear protected state. The browser never receives a private upstream token. Late responses cannot restore an obsolete session.

Overview and Servers show only API-provided information, with full uint64 uptime support. An information response confirms connectivity rather than comprehensive gameplay health. Accounts and Connections require Administrator access; the upstream service remains authoritative.

Connections retain the strong ETag read from the backend. Conflict or uncertain save results preserve the draft and require explicit reload/discard; successful replacement requires login with the new authority. Candidate401 is checked separately from the current session.

Accounts use cursor pagination with page sizes 25/50/100/200. New accounts default to Regular with API access disabled. Unknown creation outcomes block another creation until the operator refreshes and verifies the list. Administrative-session revocation names the account and requires confirmation. No global search, account edit/delete or automatic mutation retries are implemented.

## Build and deployment

```bash
npm --prefix frontend ci
scripts/build-frontend.sh
dotnet publish src/Moongate.Admin.Api -c Release -o artifacts/admin
```

The build/copy script replaces only the dedicated generated `src/Moongate.Admin.Api/wwwroot/frontend` directory. It refuses a symlinked or unmarked nonempty destination. Other `wwwroot` files and operator data are untouched. Always build/copy before evaluating the publish command; the resulting publish output must contain `wwwroot/frontend/index.html` and its assets.

Serve the published ASP.NET host at an HTTPS origin for UI and `/api`. UI deep links serve the SPA, while reserved API/health/documentation paths and missing assets keep real 404s. Production has no Swagger/OpenAPI. Without a UI build, backend operations remain available and UI routes return 404.

Generated shadcn components are committed. Their MIT variants stylesheet is vendored with its license; the generation-only CLI is excluded from the permanent dependency graph because its pinned release has an unfixed transitive advisory. Future generation can use `npm exec --package=shadcn@4.21.4 -- shadcn …`; refresh the vendored stylesheet explicitly when upgrading the generator.

## Verification

```bash
npm --prefix frontend run typecheck
npm --prefix frontend test
npm --prefix frontend run lint
npm --prefix frontend run format:check
npm --prefix frontend run build
npm --prefix frontend exec playwright -- install chromium
npm --prefix frontend run test:e2e
dotnet test MoongateAdmin.slnx -c Release
```

Vitest uses controlled HTTP responses with real React/shadcn components. Playwright runs Chromium against the production bundle with disposable HTTP fixtures, including setup/login, storage boundaries, themes, mobile navigation, configuration conflict/probe401 and uncertain creation/confirmed revocation. Screenshots and failure traces are in ignored `frontend/test-results` and uploaded by CI. Backend hosting tests independently exercise actual ASP.NET routes, assets, HTTPS and authorization boundaries. These fixtures do not prove live gRPC connectivity; that requires a configured Moongate endpoint and credentials injected at runtime.
