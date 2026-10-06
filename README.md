# Moongate Admin

![Moongate](images/moongate_logo.png)

Administration application for [Moongate](https://github.com/moongate-community/moongate).

The .NET 10 REST backend is implemented in `src/Moongate.Admin.Api`. It connects to Moongate's administration gRPC services for login/logout, server information, account listing/creation, and session revocation. The REST API uses JWT Bearer authentication. Swagger UI is available at `/swagger` in Development. The frontend remains a later stage.

Start the backend with `dotnet run --project src/Moongate.Admin.Api`. See [backend setup and REST operations](docs/backend.md) for HTTPS, endpoint configuration, JWT Bearer flows, and verification.

## Connection model

```text
Browser frontend -> Moongate Admin REST API (.NET 10) -> Moongate administration gRPC API
```

Moongate already exposes versioned administration contracts (`moongate.admin.v1`) through its optional gRPC listener, normally using TLS on port 2590. The REST backend consumes the published `Moongate.Admin.Contracts` package. Clients communicate with the REST backend; upstream access tokens stay in backend memory.

See the upstream [administration API guide](https://github.com/moongate-community/moongate/blob/develop/docs/admin-api.md) for endpoint setup, certificate trust, account permissions, and service availability.

## Supported backend operations

1. Sign in and sign out using an API-enabled Moongate account.
2. Display information for configured Login, Game, or Standalone servers.
3. List accounts with cursor pagination.
4. Create accounts with an explicit role and API access setting.
5. Revoke an account's administrative sessions.

Account administration requires an Administrator account and a Login or Standalone endpoint. The frontend will reflect these permissions, and the backend and upstream service will enforce them.

Frontend implementation is deferred. Its current proposal uses React, TypeScript, and Vite, following the upstream administration guide.

## Layout

```text
src/Moongate.Admin.Api/       REST host, sessions, and gRPC adapter
frontend/public/            Copied branding icons; application deferred
tests/Moongate.Admin.Tests/  Backend unit and integration tests
MoongateAdmin.slnx           Backend solution
```

`MoongateAdmin.slnx` contains the backend and test projects. Sessions are local to one backend instance and end on restart.

## Conventions and branding

`CODE_CONVENTION.md` and `.gitignore` are copied unchanged from Moongate. Follow the applicable coding conventions, using this project's namespaces and solution name. References to upstream services, test scripts, and convention tests describe Moongate; those components and checks do not exist here yet. The shared ReSharper settings also retain an upstream convention-test reference.

The original logo and mark are in `images/`. The favicon and Apple touch icon from Moongate's documentation website are in `frontend/public/`, ready for the frontend. The images are copied unchanged.

Use `develop` as the integration branch, feature branches for changes, and pull requests into `develop`. `main` is reserved for releases, following the copied conventions.

## Development requirements

- .NET 10 SDK.
- Node.js and npm will be needed when frontend work starts.
- An enabled Moongate administration endpoint for live integration.

Keep credentials in the designated secret store and supply them at runtime. Do not commit credentials, access tokens, private keys, or environment files. Trust the upstream certificate chain and hostname; do not disable certificate verification.

Backend implementation is tracked in [issue #3](https://github.com/moongate-community/moongate-admin/issues/3). Initialization is tracked in [issue #1](https://github.com/moongate-community/moongate-admin/issues/1).
