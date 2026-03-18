/**
 * OpenCode plugin: enforce git push restrictions.
 *
 * Push behaviour is controlled by the ISSUEPIT_AGENT_PUSH_RESTRICTION environment
 * variable (set by the execution client based on the per-repository setting):
 *
 *   Forbidden (default)  — all git push commands are blocked.
 *   WorkingOriginOnly    — push allowed only to the feature branch
 *                          (ISSUEPIT_GIT_BRANCH); force pushes and pushes to the
 *                          default branch are always denied.
 *   Allowed              — push allowed to any non-protected branch; force pushes
 *                          and pushes to the default branch are always denied.
 *   YoloMode             — no restrictions enforced by this plugin; force pushes
 *                          are still denied as a minimal safety net.
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

      // Only intercept commands that contain `git push`.
      if (!/\bgit\s+push\b/.test(command)) return;

      const restriction =
        process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION ?? "Forbidden";

      // Forbidden (default): block all git push commands.
      if (restriction === "Forbidden" || restriction === "0") {
        throw new Error(
          "[IssuePit] git push is not allowed: " +
            "IssuePit manages the git push step after the agent session ends.",
        );
      }

      // Always deny force pushes regardless of restriction level.
      if (
        /\s(--force|-f|--force-with-lease|--force-if-includes)\b/.test(command)
      ) {
        throw new Error(
          "[IssuePit] Force push is not permitted.",
        );
      }

      // YoloMode: allow all non-force pushes.
      if (restriction === "YoloMode" || restriction === "3") return;

      // WorkingOriginOnly and Allowed: deny pushes to the default branch (main/master).
      const defaultBranch = process.env.ISSUEPIT_GIT_DEFAULT_BRANCH ?? "main";
      const protectedBranchPattern = new RegExp(
        `\\b(${escapeRegex(defaultBranch)}|main|master)\\b`,
      );
      if (protectedBranchPattern.test(command)) {
        throw new Error(
          `[IssuePit] Pushing to the default branch is not permitted.`,
        );
      }

      // WorkingOriginOnly: only allow pushing to the designated feature branch.
      if (restriction === "WorkingOriginOnly" || restriction === "1") {
        const featureBranch = process.env.ISSUEPIT_GIT_BRANCH ?? "";
        if (!featureBranch) {
          throw new Error(
            "[IssuePit] git push is not allowed: no feature branch is configured for this session.",
          );
        }
        // If the command explicitly names a branch that isn't the feature branch, deny it.
        const branchRef = new RegExp(
          `\\brefs/heads/([^\\s]+)|git\\s+push(?:\\s+\\S+)?\\s+(\\S+)`,
        );
        const match = branchRef.exec(command);
        if (match) {
          const pushedBranch = match[1] ?? match[2];
          if (pushedBranch && pushedBranch !== featureBranch) {
            throw new Error(
              `[IssuePit] Pushing to '${pushedBranch}' is not allowed; ` +
                `only '${featureBranch}' is permitted in WorkingOriginOnly mode.`,
            );
          }
        }
      }
    },
  };
}

/** Escapes a string for use inside a RegExp character class or alternation. */
function escapeRegex(str) {
  return str.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
