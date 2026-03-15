/**
 * OpenCode plugin: block `git push` commands.
 *
 * IssuePit owns the git push step. Agents must never push directly;
 * the execution client pushes the branch after the agent session ends.
 *
 * Hook used:
 *   tool.execute.before — intercepts the `bash` tool before execution and
 *                         rejects any command that contains `git push`.
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

      // Match `git push` with any arguments/flags that follow.
      if (/\bgit\s+push\b/.test(command)) {
        throw new Error(
          "[IssuePit] git push is not allowed: " +
            "IssuePit manages the git push step after the agent session ends.",
        );
      }
    },
  };
}
