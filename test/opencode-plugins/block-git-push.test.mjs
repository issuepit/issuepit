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
import { test, beforeEach, afterEach } from "node:test";
import { BlockGitPushPlugin } from "../../docker/helper-containers/opencode-plugins/block-git-push.js";

// Helper: initialise the plugin (mirroring opencode's Plugin.trigger path)
// and return the tool.execute.before hook.
async function getHook() {
  const hooks = await BlockGitPushPlugin({});
  return hooks["tool.execute.before"];
}

// Helpers to set/restore ISSUEPIT_PUSH_POLICY for policy-specific tests.
let savedPolicy;
beforeEach(() => { savedPolicy = process.env.ISSUEPIT_PUSH_POLICY; });
afterEach(() => {
  if (savedPolicy === undefined) delete process.env.ISSUEPIT_PUSH_POLICY;
  else process.env.ISSUEPIT_PUSH_POLICY = savedPolicy;
});

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

// ── Forbidden policy (default, ISSUEPIT_PUSH_POLICY=0 or unset) ─────────────

test("Forbidden: blocks bare `git push`", async () => {
  delete process.env.ISSUEPIT_PUSH_POLICY;
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

test("Forbidden: blocks `git push origin main`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "0";
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

test("Forbidden: blocks `git push --force-with-lease`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "0";
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

test("Forbidden: blocks `git push -u origin HEAD`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "0";
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

test("Forbidden: blocks multi-command string containing `git push`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "0";
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

// ── Non-Forbidden policies allow push but still block force pushes ────────────

test("WorkingOriginOnly (1): allows plain `git push`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "1";
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git push origin my-feature-branch" } },
    ),
  );
});

test("Allowed (2): allows plain `git push`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "2";
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git push" } },
    ),
  );
});

test("Yolo (3): allows plain `git push`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "3";
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git push origin my-feature-branch" } },
    ),
  );
});

test("Allowed (2): blocks `git push --force`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "2";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push --force origin my-feature-branch" } },
      ),
    /Force push is not allowed/,
  );
});

test("Yolo (3): blocks `git push -f`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "3";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push -f origin my-feature-branch" } },
      ),
    /Force push is not allowed/,
  );
});

test("Allowed (2): blocks `git push --force-with-lease`", async () => {
  process.env.ISSUEPIT_PUSH_POLICY = "2";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push --force-with-lease origin my-feature-branch" } },
      ),
    /Force push is not allowed/,
  );
});

// ── Allowed commands (non-push, any policy) ───────────────────────────────────

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
