---
title: Known Issues
layout: default
nav_order: 11
parent: Developer
---

# Known Issues

---

## Aspire SSL certificate outdated

```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

---

## Aspire CLI not found

See the [Aspire CLI installation guide](https://aspire.dev/get-started/install-cli/).

```bash
export PATH="/c/Users/user/.aspire/bin:$PATH"
```

---

## Frontend is not starting inside Aspire

Aspire manages the frontend via `npm run dev`, but it requires dependencies to be installed first. Run this once before starting Aspire:

```bash
cd frontend
npm ci
```

---

## E2E tests fail with `Polly.Timeout.TimeoutRejectedException` (Aspire DCP gRPC unreachable)

**Symptom:** Running the E2E test suite (e.g. `dotnet test --filter "Category=Voice"`) fails immediately with a `Polly.Timeout.TimeoutRejectedException` or `System.Net.Http.HttpRequestException` pointing to `[::1]` (the DCP gRPC endpoint). All tests fail because `AspireFixture.InitializeAsync` cannot start the AppHost.

**Cause:** GitHub-hosted runners and some CI environments set `DOTNET_SYSTEM_NET_DISABLEIPV6=1` at the process level, which prevents .NET Aspire's orchestration client from reaching the DCP gRPC service that listens on `[::1]`. The `copilot-setup-steps.yml` workflow writes `DOTNET_SYSTEM_NET_DISABLEIPV6=0` to `/etc/environment` (system-wide), but this only takes effect in **new** shell sessions, not in the current one.

**Fix:** Set the variable to `0` in the shell where you run the tests:

```bash
export DOTNET_SYSTEM_NET_DISABLEIPV6=0
dotnet test src/IssuePit.Tests.E2E/IssuePit.Tests.E2E.csproj \
  --configuration Release \
  --filter "Category=Voice" \
  --verbosity minimal --blame-hang-timeout 4min
```

Or inline per command:

```bash
DOTNET_SYSTEM_NET_DISABLEIPV6=0 dotnet test ...
```

This must be done **every time** Aspire E2E tests are run in a shell that inherited `DOTNET_SYSTEM_NET_DISABLEIPV6=1` from the runner environment. Opening a fresh login shell (which sources `/etc/environment`) avoids the need for the explicit override.

