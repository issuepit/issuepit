#!/usr/bin/env bash
# IssuePit agent container entrypoint.
#
# Logical order (mirrors the outer runner lifecycle):
#   1. Container is already pulled and started by the execution client.
#   2. Clone the git repository (if ISSUEPIT_GIT_REMOTE_URL is provided).
#   2b. Set up git identity and create a feature branch.
#   3. Set up tools (restore npm/dotnet dependencies found in the workspace).
#   4. Log DNS queries via a local dnsmasq proxy (and note firewall state).
#   4b. Start Docker daemon (DinD) if dockerd is installed and no host socket is mounted.
#   5. Execute the CLI agent tool (captures exit code; does NOT use exec so steps 6-8 run).
#   6. Log opencode sessions (for visibility / debugging).
#   7. Handle uncommitted changes: check for unstaged files, commit if needed, push.
#   8. Emit git markers for IssueWorker to capture and use for CI/CD triggering.

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

    BASE_BRANCH="${ISSUEPIT_GIT_DEFAULT_BRANCH:-main}"

    # Determine the feature branch to work on.
    # Priority: 1. ISSUEPIT_GIT_BRANCH env var, 2. auto-generate from issue number + title.
    if [[ -z "${ISSUEPIT_GIT_BRANCH:-}" && -n "${ISSUEPIT_ISSUE_NUMBER:-}" ]]; then
        # Auto-generate branch name: verb/NNN-slugified-title
        # The verb is derived from the issue title using conventional-commit keywords.
        TITLE_LOWER=$(echo "${ISSUEPIT_ISSUE_TITLE:-}" | tr '[:upper:]' '[:lower:]')
        if echo "${TITLE_LOWER}" | grep -qE '(fix|bug|hotfix|patch|correct|repair)'; then
            VERB="fix"
        elif echo "${TITLE_LOWER}" | grep -qE '(chore|update|upgrade|refactor|clean)'; then
            VERB="chore"
        elif echo "${TITLE_LOWER}" | grep -qE '(doc|docs|document|readme)'; then
            VERB="docs"
        elif echo "${TITLE_LOWER}" | grep -qE '(test|spec|coverage)'; then
            VERB="test"
        else
            VERB="feat"
        fi
        # Slugify: lowercase, non-alphanumeric → hyphen, collapse hyphens, trim, limit to 30 chars.
        TITLE_SLUG=$(echo "${ISSUEPIT_ISSUE_TITLE:-}" \
            | tr '[:upper:]' '[:lower:]' \
            | sed 's/[^a-z0-9]/-/g' \
            | sed 's/-\+/-/g' \
            | sed 's/^-//' \
            | sed 's/-$//' \
            | cut -c1-30)
        ISSUEPIT_GIT_BRANCH="${VERB}/${ISSUEPIT_ISSUE_NUMBER}-${TITLE_SLUG}"
    fi

    FEATURE_BRANCH="${ISSUEPIT_GIT_BRANCH:-}"

    # Clone strategy:
    #   - If a feature branch is set, try cloning it directly (it may already exist from a prior run).
    #   - If that fails (branch not yet in remote), clone the base branch instead and create the
    #     feature branch locally so the agent can push it.
    if [[ -n "${FEATURE_BRANCH}" ]]; then
        echo "[entrypoint] Cloning ${ISSUEPIT_GIT_REMOTE_URL} (feature branch: ${FEATURE_BRANCH}) into ${WORKSPACE}"
        if ! git clone --depth=1 --branch "${FEATURE_BRANCH}" "${CLONE_URL}" "${WORKSPACE}" 2>/dev/null; then
            echo "[entrypoint] Feature branch '${FEATURE_BRANCH}' not found in remote; cloning base branch '${BASE_BRANCH}' and creating it locally"
            git clone --depth=1 --branch "${BASE_BRANCH}" "${CLONE_URL}" "${WORKSPACE}"
        fi
    else
        echo "[entrypoint] Cloning ${ISSUEPIT_GIT_REMOTE_URL} (branch: ${BASE_BRANCH}) into ${WORKSPACE}"
        git clone --depth=1 --branch "${BASE_BRANCH}" "${CLONE_URL}" "${WORKSPACE}"
    fi
    echo "[entrypoint] Clone complete"
fi

# ─── Step 2b: Set up git identity and feature branch ──────────────────────────

if [[ -d "${WORKSPACE}" ]]; then
    cd "${WORKSPACE}"

    # Configure git identity for commits made by the agent.
    git config user.name "IssuePit Agent"
    git config user.email "agent@issuepit.ai"

    # Create / check out the feature branch if not already on it.
    CURRENT_BRANCH=$(git branch --show-current 2>/dev/null || echo "")
    if [[ -n "${ISSUEPIT_GIT_BRANCH:-}" && "${CURRENT_BRANCH}" != "${ISSUEPIT_GIT_BRANCH}" ]]; then
        echo "[entrypoint] Checking out feature branch: ${ISSUEPIT_GIT_BRANCH}"
        git checkout -b "${ISSUEPIT_GIT_BRANCH}" 2>/dev/null \
            || git checkout "${ISSUEPIT_GIT_BRANCH}"
    fi

    echo "[entrypoint] Active branch: $(git branch --show-current)"
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
#
# Do NOT use `exec` here: we need the shell to continue running after the agent
# exits so that the post-run steps (6–8) can execute.

AGENT_EXIT_CODE=0
"$@" || AGENT_EXIT_CODE=$?

# ─── Step 6: Log opencode sessions ────────────────────────────────────────────
#
# Print the list of opencode sessions so they are visible in the container logs
# and accessible to developers for debugging or resuming work.

if command -v opencode > /dev/null 2>&1; then
    echo "[entrypoint] opencode session list:"
    opencode session list 2>/dev/null || true
fi

# ─── Step 7: Commit and push ──────────────────────────────────────────────────
#
# After the agent finishes, make sure all work is committed and pushed.
# Rules:
#   a) Stage any unstaged tracked files (untracked files may be build artifacts —
#      the agent is expected to handle .gitignore; warn if any remain).
#   b) Commit staged changes with a conventional-commit message.
#   c) Push the branch (allowed to fail — git credentials may not be configured yet).

if [[ -d "${WORKSPACE}" ]]; then
    cd "${WORKSPACE}"

    # Check for untracked (new) files that were not staged by the agent.
    UNTRACKED=$(git ls-files --others --exclude-standard 2>/dev/null || true)
    if [[ -n "${UNTRACKED}" ]]; then
        echo "[entrypoint] WARNING: untracked files found after agent run (may be build artifacts):"
        echo "${UNTRACKED}" | head -20
        echo "[entrypoint] If these should be committed, ensure the agent adds them or updates .gitignore."
    fi

    # Stage all modifications to tracked files (deletions and edits, but not untracked).
    git add -u 2>/dev/null || true

    # If there are staged changes, create a commit.
    if ! git diff --cached --quiet 2>/dev/null; then
        COMMIT_MSG="${VERB:-feat}: agent changes for issue #${ISSUEPIT_ISSUE_NUMBER:-unknown}"
        echo "[entrypoint] Committing staged changes: ${COMMIT_MSG}"
        git commit -m "${COMMIT_MSG}" 2>&1 || true
    fi

    # Push the current branch (allowed to fail if no push credentials are configured yet).
    ACTIVE_BRANCH=$(git branch --show-current 2>/dev/null || echo "")
    if [[ -n "${ACTIVE_BRANCH}" ]]; then
        echo "[entrypoint] Pushing branch '${ACTIVE_BRANCH}' to origin…"
        git push origin "${ACTIVE_BRANCH}" 2>&1 \
            || echo "[entrypoint] Push failed (allowed — credentials may not be configured or push was rejected)"
    fi
fi

# ─── Step 8: Emit git markers for IssueWorker ─────────────────────────────────
#
# IssueWorker parses these special lines from the container log stream to learn
# the final commit SHA and branch name so it can trigger and link CI/CD runs.

if [[ -d "${WORKSPACE}" ]]; then
    cd "${WORKSPACE}"

    FINAL_BRANCH=$(git branch --show-current 2>/dev/null || echo "")
    FINAL_SHA=$(git rev-parse HEAD 2>/dev/null || echo "")

    if [[ -n "${FINAL_SHA}" ]]; then
        echo "[ISSUEPIT:GIT_COMMIT_SHA]=${FINAL_SHA}"
    fi
    if [[ -n "${FINAL_BRANCH}" ]]; then
        echo "[ISSUEPIT:GIT_BRANCH]=${FINAL_BRANCH}"
    fi
fi

exit "${AGENT_EXIT_CODE}"

