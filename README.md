# Moongate Admin

![Moongate](images/moongate_logo.png)

Administration application for [Moongate](https://github.com/moongate-community/moongate).

The project will contain a .NET 10 REST Web API and a web frontend in this repository. This initial change establishes repository settings and documents the integration boundary; the applications are not implemented yet.

## Connection model

```text
Browser frontend -> Moongate Admin REST API (.NET 10) -> Moongate administration gRPC API
```

Moongate already exposes versioned administration contracts (`moongate.admin.v1`) through its optional gRPC listener, normally using TLS on port 2590. The REST backend will consume the published `Moongate.Admin.Contracts` package. The browser will communicate with the REST backend; upstream credentials and access tokens will stay in backend memory.

See the upstream [administration API guide](https://github.com/moongate-community/moongate/blob/develop/docs/admin-api.md) for endpoint setup, certificate trust, account permissions, and service availability.

## Proposed first application scope

1. Sign in and sign out using an API-enabled Moongate account.
2. Display information for configured Login, Game, or Standalone servers.
3. List accounts with cursor pagination.
4. Create accounts with an explicit role and API access setting.
5. Revoke an account's administrative sessions.

Account administration requires an Administrator account and a Login or Standalone endpoint. The frontend will reflect these permissions, and the backend and upstream service will enforce them.

The proposed frontend is React with TypeScript and Vite, following the upstream administration guide. The application design still requires review before implementation.

## Planned layout

```text
src/Moongate.Admin.Api/       REST host, sessions, and gRPC adapter
frontend/                   React and TypeScript application
tests/Moongate.Admin.Tests/  Backend unit and integration tests
MoongateAdmin.slnx           Backend solution
```

These paths describe the proposed layout; the application projects have not been generated. `MoongateAdmin.slnx.DotSettings` is prepared for that future solution.

## Conventions and branding

`CODE_CONVENTION.md` and `.gitignore` are copied unchanged from Moongate. Follow the applicable coding conventions, using this project's namespaces and solution name. References to upstream services, test scripts, and convention tests describe Moongate; those components and checks do not exist here yet. The shared ReSharper settings also retain an upstream convention-test reference.

The original logo and mark are in `images/`. The favicon and Apple touch icon from Moongate's documentation website are in `frontend/public/`, ready for the frontend. The images are copied unchanged.

Use `develop` as the integration branch, feature branches for changes, and pull requests into `develop`. `main` is reserved for releases, following the copied conventions.

## Development requirements

- .NET 10 SDK.
- Node.js 24 LTS and npm for the proposed frontend.
- An enabled Moongate administration endpoint for live integration.

Keep credentials in the designated secret store and supply them at runtime. Do not commit credentials, access tokens, private keys, or environment files. Trust the upstream certificate chain and hostname; do not disable certificate verification.

Implementation is tracked in [issue #1](https://github.com/moongate-community/moongate-admin/issues/1).
