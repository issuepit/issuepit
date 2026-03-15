/**
 * Unit tests for the block-git-push opencode plugin.
 *
 * opencode auto-loads plugins from:
 *   ~/.config/opencode/plugins/   (global, baked into the container image)
 *   .opencode/plugins/            (project-level)
 *
 * The plugin is loaded by importing the JS module and calling the exported
 * function — exactly what opencode does internally. These tests replicate
 * that loading path directly, keeping the suite dependency-free.
 *
 * Run:  node --test "test/opencode-plugins/*.test.mjs"
 */
import assert from "node:assert/strict";
import { test } from "node:test";
import { BlockGitPushPlugin } from "../../docker/helper-containers/opencode-plugins/block-git-push.js";

// Helper: initialise the plugin (mirroring opencode's Plugin.trigger path)
// and return the tool.execute.before hook.
async function getHook() {
  const hooks = await BlockGitPushPlugin({});
  return hooks["tool.execute.before"];
}

// ── Plugin contract ───────────────────────────────────────────────────────────

test("plugin exports an async function", () => {
  assert.equal(typeof BlockGitPushPlugin, "function");
  assert.equal(BlockGitPushPlugin.constructor.name, "AsyncFunction");
});

test("plugin returns a hooks object with tool.execute.before", async () => {
  const hooks = await BlockGitPushPlugin({});
  assert.ok(hooks !== null && typeof hooks === "object");
  assert.equal(typeof hooks["tool.execute.before"], "function");
});

// ── Blocked commands ──────────────────────────────────────────────────────────

test("blocks bare `git push`", async () => {
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push" } },
      ),
    /git push is not allowed/,
  );
});

test("blocks `git push origin main`", async () => {
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push origin main" } },
      ),
    /git push is not allowed/,
  );
});

test("blocks `git push --force-with-lease`", async () => {
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push --force-with-lease" } },
      ),
    /git push is not allowed/,
  );
});

test("blocks `git push -u origin HEAD`", async () => {
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push -u origin HEAD" } },
      ),
    /git push is not allowed/,
  );
});

test("blocks multi-command string containing `git push`", async () => {
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git add . && git commit -m 'fix' && git push" } },
      ),
    /git push is not allowed/,
  );
});

// ── Allowed commands ──────────────────────────────────────────────────────────

test("allows `git commit`", async () => {
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git commit -m 'fix bug'" } },
    ),
  );
});

test("allows `git status`", async () => {
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git status" } },
    ),
  );
});

test("allows `git add`", async () => {
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git add ." } },
    ),
  );
});

test("ignores non-bash tools even when args contain git push", async () => {
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "read", sessionID: "s", callID: "c" },
      { args: { command: "git push" } },
    ),
  );
});

test("handles missing args gracefully (no args.command)", async () => {
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: {} },
    ),
  );
});

test("handles null/undefined args gracefully", async () => {
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: null },
    ),
  );
});
