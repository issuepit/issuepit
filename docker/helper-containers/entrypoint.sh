#!/usr/bin/env bash
# IssuePit agent container entrypoint.
#
# Logical order:
#   1. Container is already pulled and started by the execution client.
#   2. Clone the git repository (if ISSUEPIT_GIT_REMOTE_URL is provided).
#   2b. Set up git identity and create a feature branch.
#   3. Set up tools (restore npm/dotnet dependencies found in the workspace).
#   4. Log DNS queries via a local dnsmasq proxy (and note firewall state).
#   4b. Start Docker daemon (DinD) if dockerd is installed and no host socket is mounted.
#   5. exec "$@" — hand off to the container's CMD (typically `sleep infinity`).
#
# The execution client (C#) controls all agent runs and post-processing via
# `docker exec` after this entrypoint completes setup:
#   - Runs the CLI agent tool (opencode / codex / etc.).
#   - Lists opencode sessions for debugging.
#   - Checks for uncommitted changes and emits [ISSUEPIT:HAS_UNCOMMITTED_CHANGES]=true.
#   - Emits [ISSUEPIT:GIT_COMMIT_SHA] and [ISSUEPIT:GIT_BRANCH] markers.
#   - Pushes the branch to origin.
# This design keeps the container alive so that fix runs can use the same
# opencode session (via --fork once supported) and the same git workspace.

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
        # The verb is derived from the issue title using simple keyword matching.
        # TODO: Replace keyword matching with an LLM API call so the verb and slug are
        #       semantically accurate and properly summarise the issue intent.
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

# ─── Step 2c: Install git push wrapper ─────────────────────────────────────────
#
# A lightweight wrapper is placed at /usr/local/bin/git (ahead of the real git on PATH).
# It intercepts `git push` invocations and exits with an error so that agent tools
# (opencode, codex, etc.) cannot push to the remote during their run.
# Pushing is performed explicitly by the C# execution client after the agent finishes.
#
# This is not a full security boundary — a sufficiently determined agent could locate
# and invoke the real git binary directly — but it prevents accidental pushes from
# tool-generated shell commands and conventional git usage.

REAL_GIT=$(command -v git 2>/dev/null || echo "/usr/bin/git")
cat > /usr/local/bin/git << GITWRAPPER
#!/usr/bin/env bash
# IssuePit git wrapper — blocks push; all other subcommands are forwarded unchanged.
if [[ "\${1:-}" == "push" ]]; then
    echo "[issuepit] git push is not permitted inside the agent container." >&2
    echo "[issuepit] The execution client will push the branch after your run completes." >&2
    exit 1
fi
exec "${REAL_GIT}" "\$@"
GITWRAPPER
chmod +x /usr/local/bin/git
echo "[entrypoint] git push wrapper installed (real git: ${REAL_GIT})"

# ─── Step 3: Set up tools ──────────────────────────────────────────────────────

if [[ -d "${WORKSPACE}" ]]; then
    cd "${WORKSPACE}"

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

# ─── Step 4c: Write opencode config ──────────────────────────────────────────
#
# When opencode is installed, write ~/.config/opencode/config.json so that:
#   - autoupdate=false prevents opencode from self-updating mid-run
#   - The IssuePit MCP server is registered when ISSUEPIT_MCP_URL is set
#   - Agents are configured when ISSUEPIT_OPENCODE_AGENTS_JSON is set (written to the "agent" config section)
#
# Plugin loading (opencode auto-discovers plugins — no config.json wiring needed):
#   - Baked plugins live in /root/.config/opencode/plugins/ (copied during image build)
#   - Runtime plugins are written to the same directory from ISSUEPIT_OPENCODE_PLUGINS_JSON
#     Format: [{"name": "my-plugin.js", "content": "export const MyPlugin = ..."}]
#     This allows injecting or overriding plugins without rebuilding the image.

if command -v opencode > /dev/null 2>&1 && command -v python3 > /dev/null 2>&1; then
    OPENCODE_CONFIG_DIR="${HOME}/.config/opencode"
    mkdir -p "${OPENCODE_CONFIG_DIR}"
    OPENCODE_CONFIG_FILE="${OPENCODE_CONFIG_DIR}/config.json"

    python3 - <<'OPENCODE_PYEOF'
import json, os, sys

mcp_url = os.environ.get("ISSUEPIT_MCP_URL", "")
agents_json_str = os.environ.get("ISSUEPIT_OPENCODE_AGENTS_JSON", "")
extra_mcp_json_str = os.environ.get("ISSUEPIT_OPENCODE_EXTRA_MCP_JSON", "")
opencode_port = os.environ.get("OPENCODE_PORT", "")
opencode_password = os.environ.get("OPENCODE_PASSWORD", "")
config_file = os.path.join(os.path.expanduser("~"), ".config", "opencode", "config.json")

config = {"autoupdate": False}

# Configure the server port when running in HTTP server mode (OPENCODE_PORT env var).
if opencode_port:
    try:
        config["port"] = int(opencode_port)
    except ValueError:
        print(f"[entrypoint] Warning: OPENCODE_PORT is not a valid integer: {opencode_port}", file=sys.stderr)

# Configure server authentication password (OPENCODE_PASSWORD env var).
if opencode_password:
    config["password"] = opencode_password

# Add the IssuePit MCP server when the URL is configured.
if mcp_url:
    config["mcp"] = {
        "issuepit": {
            "type": "sse",
            "url": mcp_url,
        }
    }

# Merge extra (agent-linked) MCP servers from ISSUEPIT_OPENCODE_EXTRA_MCP_JSON.
# Format: [{"name": "...", "type": "http"|"sse", "url": "...", "headers": {...}|null}, ...]
if extra_mcp_json_str:
    try:
        extra_mcps = json.loads(extra_mcp_json_str)
        if "mcp" not in config:
            config["mcp"] = {}
        for server in extra_mcps:
            key = server.get("name", "")
            if not key:
                continue
            entry = {
                "type": server.get("type", "http"),
                "url": server.get("url", ""),
            }
            headers = server.get("headers")
            if headers:
                entry["headers"] = headers
            config["mcp"][key] = entry
        print(f"[entrypoint] Extra MCP servers merged: {[s.get('name') for s in extra_mcps if s.get('name')]}")
    except Exception as e:
        print(f"[entrypoint] Warning: could not parse ISSUEPIT_OPENCODE_EXTRA_MCP_JSON: {e} (value: {extra_mcp_json_str[:200]})", file=sys.stderr)

# Add agents from ISSUEPIT_OPENCODE_AGENTS_JSON when present.
# Format expected: [{"name": "...", "model": "...", "prompt": "...", "agentType": "primary"|"subagent"|null}, ...]
# Each entry becomes a named entry in the opencode "agent" config section.
# The "agentType" field maps to the opencode "mode" property ("primary" or "subagent").
# See https://opencode.ai/docs/agents for details on opencode agent types.
if agents_json_str:
    try:
        agents = json.loads(agents_json_str)
        agent_map = {}
        for a in agents:
            agent_key = a.get("name", "").lower().replace(" ", "-")
            if agent_key:
                entry = {
                    "prompt": a.get("prompt", ""),
                }
                model = a.get("model") or None
                if model:
                    entry["model"] = model
                agent_type = a.get("agentType") or None
                if agent_type in ("primary", "subagent", "all"):
                    entry["mode"] = agent_type
                agent_map[agent_key] = entry
        if agent_map:
            config["agent"] = agent_map
    except Exception as e:
        print(f"[entrypoint] Warning: could not parse ISSUEPIT_OPENCODE_AGENTS_JSON: {e}", file=sys.stderr)

# Write runtime plugins from ISSUEPIT_OPENCODE_PLUGINS_JSON to the opencode
# global plugins directory. opencode auto-loads every *.js / *.ts file it finds
# there — no config.json wiring needed. This allows injecting or patching plugins
# at runtime even when the base image pre-dates a new plugin being added.
plugins_json_str = os.environ.get("ISSUEPIT_OPENCODE_PLUGINS_JSON", "")
if plugins_json_str:
    plugins_dir = os.path.join(os.path.expanduser("~"), ".config", "opencode", "plugins")
    os.makedirs(plugins_dir, exist_ok=True)
    try:
        runtime_plugins = json.loads(plugins_json_str)
        for i, plugin in enumerate(runtime_plugins):
            name = plugin.get("name", "")
            content = plugin.get("content", "")
            if not name or not content:
                print(f"[entrypoint] Warning: skipping runtime plugin at index {i} with missing name or content", file=sys.stderr)
                continue
            # Ensure the filename ends with .js or .ts so opencode picks it up.
            if not (name.endswith(".js") or name.endswith(".ts")):
                name = name + ".js"
                print(f"[entrypoint] Added .js suffix to runtime plugin: {name}")
            plugin_path = os.path.join(plugins_dir, name)
            with open(plugin_path, "w") as f:
                f.write(content)
            print(f"[entrypoint] Runtime plugin written: {plugin_path}")
    except Exception as e:
        print(f"[entrypoint] Warning: could not parse ISSUEPIT_OPENCODE_PLUGINS_JSON: {e}", file=sys.stderr)

with open(config_file, "w") as f:
    json.dump(config, f, indent=2)
print(f"[entrypoint] opencode config written: {config_file}")
OPENCODE_PYEOF

    # Debug: list all configured agents
    if [[ -n "${ISSUEPIT_OPENCODE_AGENTS_JSON:-}" ]]; then
        echo "[entrypoint] Configured agents (from ISSUEPIT_OPENCODE_AGENTS_JSON):"
        python3 -c "
import sys, json, os
try:
    agents = json.loads(os.environ.get('ISSUEPIT_OPENCODE_AGENTS_JSON', '[]'))
    for a in agents:
        model = a.get('model') or '(default)'
        agent_type = a.get('agentType') or '(unset)'
        print(f\"  - {a['name']} (model: {model}, type: {agent_type})\")
except Exception as e:
    print(f'  (parse error: {e})', file=sys.stderr)
" 2>&1 || true
    fi
fi

# ─── Start the container's primary process ────────────────────────────────────
#
# The execution client (C#) controls all agent runs via `docker exec`.
# exec "$@" here starts the CMD passed by the runtime — typically `sleep infinity`
# to keep the container alive. Setup is complete; C# takes over from here.

exec "$@"
