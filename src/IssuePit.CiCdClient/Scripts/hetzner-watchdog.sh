#!/bin/bash
# Self-destruct watchdog for IssuePit CI/CD Hetzner servers.
#
# Logic:
#   - A HARD_LIMIT_HOURS hard cap: the server always deletes itself after this time,
#     regardless of activity. Default: 24h.
#   - An IDLE_TIMEOUT_MINUTES soft cap: if no job has been active for this duration,
#     the server deletes itself. Default: 15 minutes.
#
# The watchdog runs every 60 seconds and checks:
#   1. Hard limit: if (now - START_TIME) > HARD_LIMIT_HOURS * 3600, delete.
#   2. Idle timeout: if ACTIVE_JOBS == 0 and (now - LAST_JOB_END) > IDLE_TIMEOUT_MINUTES * 60, delete.
#
# External processes signal activity by writing to:
#   /run/issuepit/active_jobs  — integer count of currently active jobs
#   /run/issuepit/last_job_end — unix timestamp of when the last job finished
#
# To reset the idle timer (e.g. when a new job starts): increment active_jobs.
# To trigger idle countdown: decrement active_jobs to 0 and write last_job_end.
#
# The Hetzner API token is read from /run/issuepit/.hcloud_token (root-only, mode 600)
# rather than being passed as an environment variable to avoid exposure in process
# listings or cloud-init logs.

set -euo pipefail

HARD_LIMIT_HOURS=${HARD_LIMIT_HOURS:-24}
IDLE_TIMEOUT_MINUTES=${IDLE_TIMEOUT_MINUTES:-15}
STATE_DIR="/run/issuepit"
TOKEN_FILE="$STATE_DIR/.hcloud_token"
HCLOUD_API="https://api.hetzner.cloud/v1"

mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"

START_TIME=$(date +%s)

# Write initial state
echo "0" > "$STATE_DIR/active_jobs"
echo "$START_TIME" > "$STATE_DIR/last_job_end"

log() {
    echo "[$(date -u '+%Y-%m-%dT%H:%M:%SZ')] [watchdog] $*" | tee -a /var/log/issuepit-watchdog.log
}

delete_self() {
    local reason="$1"
    log "Triggering self-destruct: $reason"

    # Retrieve this server's ID from the Hetzner metadata service
    local server_id http_code
    http_code=$(curl -s -o /tmp/hetzner-meta.json -w "%{http_code}" \
        http://169.254.169.254/hetzner/v1/metadata/instance-id 2>/dev/null || echo "000")

    if [[ "$http_code" != "200" ]]; then
        log "ERROR: Could not retrieve server ID from metadata service (HTTP $http_code). Skipping self-destruct."
        return 1
    fi
    server_id=$(cat /tmp/hetzner-meta.json 2>/dev/null || echo "")

    if [[ -z "$server_id" ]]; then
        log "ERROR: Metadata service returned empty server ID. Skipping self-destruct."
        return 1
    fi

    # Read token from the secure file (never echoed or stored in environment)
    if [[ ! -r "$TOKEN_FILE" ]]; then
        log "ERROR: Token file $TOKEN_FILE not readable. Cannot delete server $server_id."
        return 1
    fi
    local token
    token=$(cat "$TOKEN_FILE")

    log "Deleting Hetzner server $server_id via API..."
    local del_code
    del_code=$(curl -s -o /dev/null -w "%{http_code}" \
        -X DELETE \
        -H "Authorization: Bearer $token" \
        "$HCLOUD_API/servers/$server_id")

    if [[ "$del_code" =~ ^(200|201|204|404)$ ]]; then
        log "Server $server_id deletion accepted (HTTP $del_code). Shutting down."
        # Attempt immediate shutdown in case the API call is async
        sleep 5
        shutdown -h now || true
    else
        log "ERROR: Deletion API returned HTTP $del_code. Will retry next cycle."
    fi
}

log "Watchdog started. Hard limit: ${HARD_LIMIT_HOURS}h, Idle timeout: ${IDLE_TIMEOUT_MINUTES}min"

while true; do
    sleep 60

    now=$(date +%s)
    elapsed=$(( now - START_TIME ))

    # 1. Hard limit check
    hard_limit_seconds=$(( HARD_LIMIT_HOURS * 3600 ))
    if (( elapsed >= hard_limit_seconds )); then
        delete_self "hard limit of ${HARD_LIMIT_HOURS}h reached (elapsed=${elapsed}s)"
        break
    fi

    # 2. Idle timeout check
    active_jobs=$(cat "$STATE_DIR/active_jobs" 2>/dev/null || echo "0")
    last_job_end=$(cat "$STATE_DIR/last_job_end" 2>/dev/null || echo "$START_TIME")
    idle_seconds=$(( now - last_job_end ))
    idle_timeout_seconds=$(( IDLE_TIMEOUT_MINUTES * 60 ))

    if [[ "$active_jobs" -eq 0 ]] && (( idle_seconds >= idle_timeout_seconds )); then
        delete_self "idle for ${idle_seconds}s (threshold=${idle_timeout_seconds}s, no active jobs)"
        break
    fi

    remaining_hard=$(( hard_limit_seconds - elapsed ))
    log "Status: active_jobs=$active_jobs, idle=${idle_seconds}s/${idle_timeout_seconds}s, hard_limit_remaining=${remaining_hard}s"
done
