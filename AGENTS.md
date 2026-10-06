# Repository instructions

## Scope

Keep the REST backend and administration frontend in this repository. Moongate itself lives in a separate repository and exposes the `moongate.admin.v1` gRPC interface.

The .NET 10 backend and its tests are implemented. Frontend application code is deferred; `frontend/public` currently holds copied branding icons. See `docs/backend.md` for implemented behavior and operating instructions.

## Architecture

- Keep the architecture simple: frontend, REST host, and a focused gRPC adapter.
- Use .NET 10 for the REST API.
- Use published Moongate administration contracts; do not reference another local checkout from project files.
- Route browser calls through the REST backend. Keep upstream access tokens in backend memory.
- Preserve upstream authorization, account pagination, token expiration, and server role restrictions.
- Do not connect directly to Moongate's PostgreSQL or Redis databases.
- Validate certificate trust and hostnames. Never add a callback that accepts every certificate.
- Do not automatically retry account creation or other mutations.

## Code and verification

- Follow `CODE_CONVENTION.md`, `.editorconfig`, and `MoongateAdmin.slnx.DotSettings`. Apply the copied conventions to this project's namespaces and solution name; upstream-only components and test commands are not implemented here.
- Use one primary C# type per file, file-scoped namespaces, explicit constructors, and block-bodied methods.
- Match namespaces to folder paths. Put contracts in `Interfaces`, DTOs in `Data`, enums in `Types`, and implementations in `Services`.
- Document interfaces and their members with English XML documentation.
- Use test-driven development for application behavior. Test authorization, sessions, error mapping, and integration contracts.
- Keep documentation honest about which projects and commands actually exist.
- Use English for scripts and conventional commit messages.
- Keep plans and design specifications outside the repository, under the user's plans directory.
- Keep credentials in the designated secret store and inject them at runtime.
- Branch from `develop` and open pull requests into `develop`. Reserve `main` for releases.
