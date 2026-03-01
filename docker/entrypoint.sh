#!/usr/bin/env bash
# IssuePit agent container entrypoint.
#
# Logical order (mirrors the outer runner lifecycle):
#   1. Container is already pulled and started by the execution client.
#   2. Clone the git repository (if ISSUEPIT_GIT_REMOTE_URL is provided).
#   3. Set up tools (restore npm/dotnet dependencies found in the workspace).
#   4. Internet access is already restricted externally via DNS when DisableInternet=true.
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

# ─── Step 5: Execute the CLI agent tool ───────────────────────────────────────

exec "$@"
