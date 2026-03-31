---
title: Known Issues
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
