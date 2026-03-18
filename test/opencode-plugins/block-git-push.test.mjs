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

// ── Forbidden mode (default) ──────────────────────────────────────────────────

test("Forbidden: blocks bare `git push`", async () => {
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
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
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
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
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
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
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
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
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
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

test("Forbidden: numeric value '0' also blocks push", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "0";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push" } },
      ),
    /git push is not allowed/,
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
});

// ── Force-push always denied ──────────────────────────────────────────────────

test("Allowed: denies --force push", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "Allowed";
  process.env.ISSUEPIT_GIT_DEFAULT_BRANCH = "main";
  process.env.ISSUEPIT_GIT_BRANCH = "feat/123-fix";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push --force origin feat/123-fix" } },
      ),
    /Force push is not permitted/,
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  delete process.env.ISSUEPIT_GIT_DEFAULT_BRANCH;
  delete process.env.ISSUEPIT_GIT_BRANCH;
});

test("Allowed: denies -f push", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "Allowed";
  process.env.ISSUEPIT_GIT_DEFAULT_BRANCH = "main";
  process.env.ISSUEPIT_GIT_BRANCH = "feat/123-fix";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push -f origin feat/123-fix" } },
      ),
    /Force push is not permitted/,
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  delete process.env.ISSUEPIT_GIT_DEFAULT_BRANCH;
  delete process.env.ISSUEPIT_GIT_BRANCH;
});

test("YoloMode: denies force push even in yolo mode", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "YoloMode";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push --force origin main" } },
      ),
    /Force push is not permitted/,
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
});

// ── WorkingOriginOnly mode ────────────────────────────────────────────────────

test("WorkingOriginOnly: allows push to feature branch", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "WorkingOriginOnly";
  process.env.ISSUEPIT_GIT_DEFAULT_BRANCH = "main";
  process.env.ISSUEPIT_GIT_BRANCH = "feat/123-fix";
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git push origin feat/123-fix" } },
    ),
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  delete process.env.ISSUEPIT_GIT_DEFAULT_BRANCH;
  delete process.env.ISSUEPIT_GIT_BRANCH;
});

test("WorkingOriginOnly: denies push to default branch", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "WorkingOriginOnly";
  process.env.ISSUEPIT_GIT_DEFAULT_BRANCH = "main";
  process.env.ISSUEPIT_GIT_BRANCH = "feat/123-fix";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push origin main" } },
      ),
    /default branch/,
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  delete process.env.ISSUEPIT_GIT_DEFAULT_BRANCH;
  delete process.env.ISSUEPIT_GIT_BRANCH;
});

test("WorkingOriginOnly: denies push to a branch other than the feature branch", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "WorkingOriginOnly";
  process.env.ISSUEPIT_GIT_DEFAULT_BRANCH = "main";
  process.env.ISSUEPIT_GIT_BRANCH = "feat/123-fix";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push origin refs/heads/other-branch" } },
      ),
    /WorkingOriginOnly mode/,
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  delete process.env.ISSUEPIT_GIT_DEFAULT_BRANCH;
  delete process.env.ISSUEPIT_GIT_BRANCH;
});

test("WorkingOriginOnly: denies push when no feature branch configured", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "WorkingOriginOnly";
  delete process.env.ISSUEPIT_GIT_BRANCH;
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push" } },
      ),
    /no feature branch/,
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
});

// ── Allowed mode ──────────────────────────────────────────────────────────────

test("Allowed: allows push to feature branch", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "Allowed";
  process.env.ISSUEPIT_GIT_DEFAULT_BRANCH = "main";
  process.env.ISSUEPIT_GIT_BRANCH = "feat/123-fix";
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git push origin feat/123-fix" } },
    ),
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  delete process.env.ISSUEPIT_GIT_DEFAULT_BRANCH;
  delete process.env.ISSUEPIT_GIT_BRANCH;
});

test("Allowed: allows push to any non-protected branch", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "Allowed";
  process.env.ISSUEPIT_GIT_DEFAULT_BRANCH = "main";
  process.env.ISSUEPIT_GIT_BRANCH = "feat/123-fix";
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git push origin other-feature" } },
    ),
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  delete process.env.ISSUEPIT_GIT_DEFAULT_BRANCH;
  delete process.env.ISSUEPIT_GIT_BRANCH;
});

test("Allowed: denies push to main", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "Allowed";
  process.env.ISSUEPIT_GIT_DEFAULT_BRANCH = "main";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push origin main" } },
      ),
    /default branch/,
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  delete process.env.ISSUEPIT_GIT_DEFAULT_BRANCH;
});

test("Allowed: denies push to master", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "Allowed";
  process.env.ISSUEPIT_GIT_DEFAULT_BRANCH = "master";
  const hook = await getHook();
  await assert.rejects(
    () =>
      hook(
        { tool: "bash", sessionID: "s", callID: "c" },
        { args: { command: "git push origin master" } },
      ),
    /default branch/,
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  delete process.env.ISSUEPIT_GIT_DEFAULT_BRANCH;
});

// ── YoloMode ──────────────────────────────────────────────────────────────────

test("YoloMode: allows push to any branch", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "YoloMode";
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git push origin some-branch" } },
    ),
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
});

test("YoloMode: numeric value '3' also allows push", async () => {
  process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION = "3";
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git push origin main" } },
    ),
  );
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
});

// ── Allowed commands (always) ─────────────────────────────────────────────────

test("allows `git commit`", async () => {
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git commit -m 'fix bug'" } },
    ),
  );
});

test("allows `git status`", async () => {
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git status" } },
    ),
  );
});

test("allows `git add`", async () => {
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: { command: "git add ." } },
    ),
  );
});

test("ignores non-bash tools even when args contain git push", async () => {
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "read", sessionID: "s", callID: "c" },
      { args: { command: "git push" } },
    ),
  );
});

test("handles missing args gracefully (no args.command)", async () => {
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: {} },
    ),
  );
});

test("handles null/undefined args gracefully", async () => {
  delete process.env.ISSUEPIT_AGENT_PUSH_RESTRICTION;
  const hook = await getHook();
  await assert.doesNotReject(() =>
    hook(
      { tool: "bash", sessionID: "s", callID: "c" },
      { args: null },
    ),
  );
});
