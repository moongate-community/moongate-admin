# Moongate Admin

REST API for administering a [Moongate](https://github.com/moongate-community/moongate) server. It calls Moongate's `moongate.admin.v1` gRPC services; a future browser frontend talks only to this API.

```text
Browser -> Moongate Admin REST API (.NET 10, JWT Bearer) -> Moongate administration gRPC API
```

Operations: login/logout, server information, account listing (cursor pagination), account creation, administrative session revocation. See [docs/backend.md](docs/backend.md) for configuration, flows and errors.

Run: `dotnet run --project src/Moongate.Admin.Api` (Development listens on `https://localhost:7080`, Swagger at `/swagger`).

Layout: `src/Moongate.Admin.Api` (host), `tests/Moongate.Admin.Tests` (xUnit with an in-process fake gRPC upstream), `frontend/` (React app), `MoongateAdmin.slnx`.

Requirements: .NET SDK 10.0.401, Node.js 24 with npm for the frontend, and an enabled Moongate administration endpoint (see the upstream [administration guide](https://github.com/moongate-community/moongate/blob/develop/docs/admin-api.md)). Keep credentials in your secret store and supply them at runtime; never commit them.

## Frontend

`frontend/` is a React + TypeScript + Vite app using shadcn/ui and Tailwind. It signs in to the REST API, shows servers, lists and creates accounts and revokes account sessions. The session token lives only in memory, so reloading the page signs you out.

Develop: run the API (`dotnet run --project src/Moongate.Admin.Api`), trust the dev certificate once (`dotnet dev-certs https --trust`), then `cd frontend && npm install && npm run dev`. Vite proxies `/api` and `/health` to `https://localhost:7080`.

Production: `cd frontend && npm ci && npm run build`. The API serves the build when `Frontend:Path` points at it (default `../../frontend/dist`, relative to the API project). Everything is served from one origin, so no CORS is needed.

The integration branch is `develop`; `main` is for releases.
