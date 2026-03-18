/**
 * OpenCode plugin: enforce ISSUEPIT_PUSH_POLICY for `git push` commands.
 *
 * The push policy is set by the IssuePit execution client via the ISSUEPIT_PUSH_POLICY
 * environment variable:
 *
 *   0 = Forbidden (default) — git push is blocked; IssuePit pushes the branch itself.
 *   1 = WorkingOriginOnly   — agents may push; only to Working-mode origins.
 *   2 = Allowed             — agents may push to any non-ReadOnly origin.
 *   3 = Yolo                — agents may push unconditionally.
 *
 * Regardless of the non-Forbidden policies, force pushes (--force / -f /
 * --force-with-lease / --force-if-includes) are always blocked here for extra safety.
 * The in-container git wrapper (installed by the entrypoint) enforces the same rules
 * at the OS level; this plugin provides an earlier, more informative error for opencode.
 *
 * Hook used:
 *   tool.execute.before — intercepts the `bash` tool before execution.
 *
 * @param {import("@opencode-ai/plugin").PluginInput} _input
 * @returns {Promise<import("@opencode-ai/plugin").Hooks>}
 */
export async function BlockGitPushPlugin(_input) {
  return {
    /** @type {import("@opencode-ai/plugin").Hooks["tool.execute.before"]} */
    "tool.execute.before": async (input, output) => {
      if (input.tool !== "bash") return;

      const command = output.args?.command ?? "";

      // Only apply restrictions to commands containing `git push`.
      if (!/\bgit\s+push\b/.test(command)) return;

      const policy = parseInt(process.env.ISSUEPIT_PUSH_POLICY ?? "0", 10);

      if (policy === 0) {
        // Forbidden: block all git push commands.
        throw new Error(
          "[IssuePit] git push is not allowed (push policy: Forbidden). " +
            "IssuePit manages the git push step after the agent session ends.",
        );
      }

      // Non-Forbidden: always block force pushes regardless of the policy value.
      if (/\s(--force|-f|--force-with-lease|--force-if-includes)\b/.test(command)) {
        throw new Error(
          "[IssuePit] Force push is not allowed. " +
            "IssuePit enforces no-force-push as a safety guard on all push policies.",
        );
      }

      // For non-Forbidden policies the push is allowed (the git wrapper handles
      // further guards like blocking pushes to the default branch).
    },
  };
}
