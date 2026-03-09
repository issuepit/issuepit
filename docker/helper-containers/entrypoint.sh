#!/usr/bin/env bash
# IssuePit agent container entrypoint.
#
# Logical order (mirrors the outer runner lifecycle):
#   1. Container is already pulled and started by the execution client.
#   2. Clone the git repository (if ISSUEPIT_GIT_REMOTE_URL is provided).
#   3. Set up tools (restore npm/dotnet dependencies found in the workspace).
#   4. Log DNS queries via a local dnsmasq proxy (and note firewall state).
#   4b. Start Docker daemon (DinD) if dockerd is installed and no host socket is mounted.
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
# A local dnsmasq proxy is started to log every DNS query to stderr (visible via
# `docker logs`) regardless of the DisableInternet setting.
#
# When DisableInternet=true dnsmasq also enforces an allowlist: all domains are
# blocked by default (--address=/#/0.0.0.0) and only the following development
# domains are forwarded to the upstream resolver:
#
#   github.com, *.github.com              — git clone, GitHub API
#   npmjs.org, *.npmjs.org                — npm registry
#   nuget.org, *.nuget.org                — NuGet packages
#   microsoft.com, *.microsoft.com        — .NET / Aspire docs, MCR images
#   ghcr.io, *.ghcr.io                    — GitHub Container Registry
#   docker.io, *.docker.io                — Docker Hub
#   aspire.dev, *.aspire.dev              — Aspire documentation

# Domains allowed through when DisableInternet=true.
ALLOWED_DOMAINS=(
    github.com
    npmjs.org
    nuget.org
    microsoft.com
    ghcr.io
    docker.io
    aspire.dev
)

if command -v dnsmasq > /dev/null 2>&1; then
    UPSTREAM_DNS=$(grep '^nameserver' /etc/resolv.conf 2>/dev/null | head -1 | awk '{print $2}')

    # Only set up the proxy when there is a real upstream to forward to.
    if [[ -n "${UPSTREAM_DNS}" && "${UPSTREAM_DNS}" != "127.0.0.1" ]]; then
        DNSMASQ_ARGS=(
            --no-hosts
            --no-resolv
            --log-queries
            --log-facility=-
            --listen-address=127.0.0.1
            --bind-interfaces
            --pid-file=/tmp/dnsmasq.pid
        )

        if [[ "${ISSUEPIT_DISABLE_INTERNET:-false}" == "true" ]]; then
            # Block all domains by default, then allow the development allowlist.
            DNSMASQ_ARGS+=(--address=/#/0.0.0.0)
            for DOMAIN in "${ALLOWED_DOMAINS[@]}"; do
                DNSMASQ_ARGS+=(--server=/"${DOMAIN}"/"${UPSTREAM_DNS}")
            done
        else
            # Forward everything to the upstream resolver (logging only).
            DNSMASQ_ARGS+=(--server="${UPSTREAM_DNS}")
        fi

        dnsmasq "${DNSMASQ_ARGS[@]}"

        # Route all container DNS lookups through the local logging proxy.
        echo "nameserver 127.0.0.1" > /etc/resolv.conf

        echo "[entrypoint] DNS proxy started (upstream: ${UPSTREAM_DNS}, DisableInternet=${ISSUEPIT_DISABLE_INTERNET:-false})"
    fi
fi

# ─── Step 4b: Start Docker daemon (DinD) ──────────────────────────────────────
#
# When the helper image includes Docker Engine (dockerd binary) and the host
# socket has NOT been bind-mounted (i.e. running in true DinD / Privileged mode),
# start an in-container daemon so agent tools like act can spawn job containers
# without touching the host Docker daemon.
#
# Skipped when:
#   - dockerd is not installed in the image (e.g. helper-opencode, helper-base)
#   - /var/run/docker.sock already exists (host socket was mounted — legacy mode)

if command -v dockerd > /dev/null 2>&1 && [ ! -e /var/run/docker.sock ]; then
    echo "[entrypoint] Starting dockerd (DinD)..."
    dockerd > /tmp/dockerd.log 2>&1 &
    TIMEOUT=60
    while [ $TIMEOUT -gt 0 ] && ! docker info > /dev/null 2>&1; do
        sleep 1; TIMEOUT=$((TIMEOUT-1))
    done
    docker info > /dev/null 2>&1 \
        && echo "[entrypoint] dockerd ready" \
        || { echo "[entrypoint] dockerd failed to start"; cat /tmp/dockerd.log; exit 1; }
fi

# ─── Step 5: Execute the CLI agent tool ───────────────────────────────────────

exec "$@"
