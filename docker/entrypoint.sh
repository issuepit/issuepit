#!/usr/bin/env bash
# IssuePit agent container entrypoint.
#
# Logical order (mirrors the outer runner lifecycle):
#   1. Container is already pulled and started by the execution client.
#   2. Clone the git repository (if ISSUEPIT_GIT_REMOTE_URL is provided).
#   3. Set up tools (restore npm/dotnet dependencies found in the workspace).
#   4. Log DNS queries via a local dnsmasq proxy (and note firewall state).
#   5. Execute the CLI agent tool (passes all arguments through).

set -euo pipefail

WORKSPACE="/workspace"

# ─── Step 2: Clone git repository ─────────────────────────────────────────────

if [[ -n "${ISSUEPIT_GIT_REMOTE_URL:-}" ]]; then
    CLONE_URL="${ISSUEPIT_GIT_REMOTE_URL}"

    # Inject credentials into the URL when both username and token are provided.
    if [[ -n "${ISSUEPIT_GIT_AUTH_USERNAME:-}" && -n "${ISSUEPIT_GIT_AUTH_TOKEN:-}" ]]; then
        # Build authenticated URL: https://user:token@host/path
        CLONE_URL=$(echo "${ISSUEPIT_GIT_REMOTE_URL}" | \
            sed "s|https://|https://${ISSUEPIT_GIT_AUTH_USERNAME}:${ISSUEPIT_GIT_AUTH_TOKEN}@|")
    fi

    BRANCH="${ISSUEPIT_GIT_BRANCH:-${ISSUEPIT_GIT_DEFAULT_BRANCH:-main}}"

    echo "[entrypoint] Cloning ${ISSUEPIT_GIT_REMOTE_URL} (branch: ${BRANCH}) into ${WORKSPACE}"
    git clone --depth=1 --branch "${BRANCH}" "${CLONE_URL}" "${WORKSPACE}"
    echo "[entrypoint] Clone complete"
fi

# ─── Step 3: Set up tools ──────────────────────────────────────────────────────

if [[ -d "${WORKSPACE}" ]]; then
    cd "${WORKSPACE}"

    # Restore Node.js dependencies
    if [[ -f "package.json" ]]; then
        echo "[entrypoint] Running npm install"
        npm install --prefer-offline
    fi

    # Restore .NET dependencies
    if compgen -G "*.sln" > /dev/null || compgen -G "**/*.csproj" > /dev/null || find . -maxdepth 3 -name "*.csproj" -quit 2>/dev/null | grep -q .; then
        echo "[entrypoint] Running dotnet restore"
        dotnet restore
    fi
fi

# ─── Step 4: DNS firewall / logging ───────────────────────────────────────────
#
# When DisableInternet=true the execution client points the container's DNS resolver
# to a restricted DNS server via Docker's --dns flag.  That server blocks general
# internet while keeping the following development domains reachable:
#
#   github.com, *.github.com              — git clone, GitHub API
#   registry.npmjs.org, *.npmjs.org       — npm registry
#   api.nuget.org, *.nuget.org            — NuGet packages
#   mcr.microsoft.com                     — .NET / Playwright images
#   ghcr.io, *.ghcr.io                    — GitHub Container Registry
#   registry-1.docker.io, *.docker.io    — Docker Hub
#
# We also start a local dnsmasq proxy (forwarding to the upstream resolver) so
# that ALL DNS queries made during this run appear in the container's log output
# (stderr → captured by `docker logs`).  This is available regardless of whether
# DisableInternet is true or false.

if command -v dnsmasq > /dev/null 2>&1; then
    UPSTREAM_DNS=$(grep '^nameserver' /etc/resolv.conf 2>/dev/null | head -1 | awk '{print $2}')

    # Only set up the proxy when there is a real upstream to forward to.
    if [[ -n "${UPSTREAM_DNS}" && "${UPSTREAM_DNS}" != "127.0.0.1" ]]; then
        # Start dnsmasq as a local forwarding proxy that logs every DNS query to stderr.
        dnsmasq \
            --no-hosts \
            --no-resolv \
            --server="${UPSTREAM_DNS}" \
            --log-queries \
            --log-facility=- \
            --listen-address=127.0.0.1 \
            --bind-interfaces \
            --pid-file=/tmp/dnsmasq.pid

        # Route all container DNS lookups through the local logging proxy.
        echo "nameserver 127.0.0.1" > /etc/resolv.conf

        echo "[entrypoint] DNS logging proxy started (upstream: ${UPSTREAM_DNS}, DisableInternet=${ISSUEPIT_DISABLE_INTERNET:-false})"
    fi
fi

# ─── Step 5: Execute the CLI agent tool ───────────────────────────────────────

exec "$@"
