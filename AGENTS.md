# Repository instructions

## Scope

The .NET 10 REST API lives in `src/Moongate.Admin.Api`; Moongate itself is a separate repository exposing the `moongate.admin.v1` gRPC interface. The frontend is deferred.

## Architecture

- Keep it simple: REST host plus a focused gRPC adapter.
- Browser calls use JWT Bearer. Upstream gRPC tokens stay in backend memory and out of JWT claims, responses and logs.
- Preserve upstream authorization, pagination, token expiry and role restrictions.
- Never accept every certificate; validate trust and hostname. No automatic retry of mutations.
- Do not connect to Moongate's PostgreSQL or Redis.
- Server addresses come only from configuration (appsettings/environment).

## Code and verification

- Follow `CODE_CONVENTION.md` and `.editorconfig`: one primary type per file, file-scoped namespaces, explicit constructors, block-bodied methods, namespaces matching folders, interface docs in English.
- Test first. Cover authorization, JWT sessions, error mapping and the REST contract.
- Keep documentation honest about what exists.
- English for scripts and conventional commits; no AI attribution in commits or PRs.
- Plans and specs live outside the repository.
- Credentials stay in the secret store and are injected at runtime.
- Branch from `develop`, open PRs into `develop`; `main` is for releases.
