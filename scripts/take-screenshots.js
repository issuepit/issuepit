#!/usr/bin/env node
// @ts-check
/**
 * Playwright screenshot script for IssuePit user documentation.
 *
 * Usage:
 *   FRONTEND_URL=http://localhost:3000 node scripts/take-screenshots.js [output-dir]
 *
 * Options (environment variables):
 *   FRONTEND_URL          Frontend base URL (default: http://localhost:3000)
 *   API_URL               API base URL (default: http://localhost:5000)
 *   SCREENSHOT_USERNAME   Pre-seeded username (Aspire: alice)
 *   SCREENSHOT_PASSWORD   Pre-seeded password (Aspire: alice)
 *   GITHUB_TOKEN          GitHub token — required for --create-pr
 *   GITHUB_REPO           owner/repo for the PR (default: issuepit/issuepit)
 *   PR_BRANCH             Branch to commit screenshots to (default: docs/update-screenshots-<timestamp>)
 *   PR_BASE               Base branch for the PR (default: main)
 *
 * Flags:
 *   --create-pr           Commit screenshots and open a GitHub PR after taking them.
 *
 * The script:
 *   1. Logs in (or registers) a user.
 *   2. Seeds minimal demo data when no pre-seeded user is configured.
 *   3. Takes screenshots of each main UI page + the /demo component pages.
 *   4. Saves them to <output-dir> (defaults to docs/assets/screenshots).
 *   5. If --create-pr: pushes a new branch and opens a draft PR on GitHub.
 *
 * When running against an Aspire-managed stack the Migrator already seeds demo data
 * (alice/alice with the Acme Corp org). Set SCREENSHOT_USERNAME=alice and
 * SCREENSHOT_PASSWORD=alice to log in as that user and skip manual seeding.
 */

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const FRONTEND_URL = (process.env.FRONTEND_URL || 'http://localhost:3000').replace(/\/$/, '');
const API_URL = (process.env.API_URL || 'http://localhost:5000').replace(/\/$/, '');
const OUTPUT_DIR = process.argv[2] || path.join(__dirname, '..', 'docs', 'assets', 'screenshots');

// When SCREENSHOT_USERNAME is set the script logs in with those credentials and
// skips manual user registration and data seeding (the Aspire Migrator handles seeding).
const SEEDED_USERNAME = process.env.SCREENSHOT_USERNAME || '';
const SEEDED_PASSWORD = process.env.SCREENSHOT_PASSWORD || '';
const USE_SEEDED_USER = !!SEEDED_USERNAME;

const USERNAME = USE_SEEDED_USER ? SEEDED_USERNAME : `docs${Math.random().toString(36).slice(2, 10)}`;
const PASSWORD = USE_SEEDED_USER ? SEEDED_PASSWORD : 'DocsPass1!';

// PR creation (--create-pr flag)
const CREATE_PR = process.argv.includes('--create-pr');
const GITHUB_TOKEN = process.env.GITHUB_TOKEN || '';
const GITHUB_REPO = process.env.GITHUB_REPO || 'issuepit/issuepit';
const PR_BRANCH = process.env.PR_BRANCH || `docs/update-screenshots-${Date.now()}`;
const PR_BASE = process.env.PR_BASE || 'main';

async function waitForBackend(maxWaitMs = 120_000) {
  const start = Date.now();
  while (Date.now() - start < maxWaitMs) {
    try {
      const res = await fetch(`${API_URL}/health`);
      if (res.ok) return;
    } catch {
      // not yet ready
    }
    await new Promise(r => setTimeout(r, 2000));
  }
  throw new Error(`Backend at ${API_URL} did not become healthy within ${maxWaitMs}ms`);
}

async function seedData(apiClient, tenantId) {
  const headers = {
    'Content-Type': 'application/json',
    'X-Tenant-Id': tenantId,
  };

  // Create a project
  const projectRes = await apiClient.post(`${API_URL}/api/projects`, {
    headers,
    data: JSON.stringify({ name: 'demo-app', description: 'Demo project for screenshots' }),
  });
  const project = await projectRes.json();

  // Create some issues
  const issueStatuses = ['todo', 'in_progress', 'done'];
  const issueTitles = [
    'Implement user authentication',
    'Add dark mode support',
    'Fix navigation performance',
    'Write API documentation',
    'Set up CI/CD pipeline',
  ];
  for (let i = 0; i < issueTitles.length; i++) {
    await apiClient.post(`${API_URL}/api/projects/${project.id}/issues`, {
      headers,
      data: JSON.stringify({
        title: issueTitles[i],
        description: `Description for: ${issueTitles[i]}`,
        status: issueStatuses[i % issueStatuses.length],
        priority: ['low', 'medium', 'high'][i % 3],
      }),
    });
  }

  // Create an agent mode
  await apiClient.post(`${API_URL}/api/agents`, {
    headers,
    data: JSON.stringify({
      name: 'Code Agent',
      systemPrompt:
        'You are a senior TypeScript developer. Implement the described feature following existing code conventions.',
      dockerImage: 'ghcr.io/issuepit/issuepit-helper-opencode-act:latest',
      queue: 'Code',
    }),
  });

  return project;
}

async function screenshot(page, name) {
  const file = path.join(OUTPUT_DIR, `${name}.png`);
  await page.waitForLoadState('networkidle');
  await page.screenshot({ path: file, fullPage: false });
  console.log(`  ✓  ${file}`);
}

// Screenshot after an interactive action (e.g. modal open, sidebar expanded).
// Does NOT wait for networkidle — just lets animations settle.
async function screenshotState(page, name) {
  const file = path.join(OUTPUT_DIR, `${name}.png`);
  await page.waitForTimeout(500);
  await page.screenshot({ path: file, fullPage: false });
  console.log(`  ✓  ${file}`);
}

async function main() {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });

  console.log(`Waiting for backend at ${API_URL}…`);
  await waitForBackend();

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    deviceScaleFactor: 2,
  });

  // --- Auth ---
  const loginPage = await context.newPage();
  await loginPage.goto(`${FRONTEND_URL}/login`);
  await loginPage.waitForLoadState('networkidle');

  if (USE_SEEDED_USER) {
    console.log(`Logging in as seeded user ${USERNAME}…`);
    await loginPage.fill("input[autocomplete='username']", USERNAME);
    await loginPage.fill("input[autocomplete='current-password']", PASSWORD);
    await loginPage.click("button[type='submit']");
  } else {
    console.log(`Registering fresh user ${USERNAME}…`);
    await loginPage.click("button:has-text('Create account')");
    await loginPage.fill("input[autocomplete='username']", USERNAME);
    await loginPage.fill("input[autocomplete='new-password']", PASSWORD);
    await loginPage.click("button[type='submit']");
  }
  await loginPage.waitForURL(`${FRONTEND_URL}/`, { timeout: 20_000 });

  // Get the default tenant ID from the admin API (seeded by Migrator with hostname "localhost").
  // context.request shares cookies with the browser context so authenticated calls work.
  const apiClient = context.request;
  const tenantsRes = await apiClient.get(`${API_URL}/api/admin/tenants`);
  const tenants = await tenantsRes.json();
  const defaultTenant = tenants.find((t) => t.hostname === 'localhost');
  if (!defaultTenant) {
    throw new Error(`Default 'localhost' tenant not found. Tenants: ${JSON.stringify(tenants)}`);
  }
  const tenantId = defaultTenant.id;

  await loginPage.close();

  // --- Seed data (only when not using a pre-seeded user) ---
  if (!USE_SEEDED_USER) {
    console.log('Seeding demo data…');
    await seedData(apiClient, tenantId);
  }

  // --- Screenshots ---
  console.log(`Taking screenshots → ${OUTPUT_DIR}`);
  const page = await context.newPage();

  await page.goto(FRONTEND_URL);
  await screenshot(page, 'dashboard');

  await page.goto(`${FRONTEND_URL}/projects`);
  await screenshot(page, 'projects');

  await page.goto(`${FRONTEND_URL}/issues`);
  await screenshot(page, 'issues');

  // Get the first available project for project-specific screenshots
  const projectsRes = await apiClient.get(`${API_URL}/api/projects`, {
    headers: tenantId ? { 'X-Tenant-Id': tenantId } : {},
  });
  const projects = await projectsRes.json();

  await page.goto(`${FRONTEND_URL}/agents`);
  await screenshot(page, 'agents');

  await page.goto(`${FRONTEND_URL}/config/keys`);
  await screenshot(page, 'api-keys');

  await page.goto(`${FRONTEND_URL}/todos`);
  await screenshot(page, 'todos');

  await page.goto(`${FRONTEND_URL}/runs`);
  await screenshot(page, 'runs');

  if (Array.isArray(projects) && projects.length > 0) {
    const proj = projects[0];

    // --- Project dashboard ---
    await page.goto(`${FRONTEND_URL}/projects/${proj.id}`);
    await screenshot(page, 'project-dashboard');

    // Project dashboard — draft/customize mode
    try {
      await page.evaluate(() => window.scrollTo(0, 0));
      await page.getByText('Customize dashboard').click({ timeout: 3000 });
      await page.evaluate(() => window.scrollTo(0, 0));
      await screenshotState(page, 'project-dashboard-draft');
      await page.getByRole('button', { name: 'Cancel' }).click();
      await page.waitForTimeout(300);
    } catch (e) {
      console.warn('  ⚠  project-dashboard-draft skipped:', e.message);
    }

    // --- Kanban board ---
    await page.goto(`${FRONTEND_URL}/projects/${proj.id}/kanban`);
    await screenshot(page, 'kanban');

    // Kanban — issue preview sidebar (click first card)
    try {
      await page.locator('div.cursor-pointer[draggable="true"]').first().click({ timeout: 3000 });
      await screenshotState(page, 'kanban-card-preview');
      await page.keyboard.press('Escape');
      await page.waitForTimeout(200);
    } catch (e) {
      console.warn('  ⚠  kanban-card-preview skipped:', e.message);
    }

    // Kanban — new board dialog (lane property selector)
    try {
      await page.getByText('+ Board').click({ timeout: 3000 });
      await screenshotState(page, 'kanban-new-board');
      await page.keyboard.press('Escape');
      await page.waitForTimeout(200);
    } catch (e) {
      console.warn('  ⚠  kanban-new-board skipped:', e.message);
    }

    // Kanban — transitions dialog
    try {
      await page.getByRole('button', { name: 'Transitions' }).click({ timeout: 3000 });
      await screenshotState(page, 'kanban-transitions');
      await page.keyboard.press('Escape');
      await page.waitForTimeout(200);
    } catch (e) {
      console.warn('  ⚠  kanban-transitions skipped:', e.message);
    }

    // --- CI/CD and milestones ---
    await page.goto(`${FRONTEND_URL}/projects/${proj.id}/ci-cd`);
    await screenshot(page, 'cicd');

    await page.goto(`${FRONTEND_URL}/projects/${proj.id}/milestones`);
    await screenshot(page, 'milestones');

    // --- Test History ---
    await page.goto(`${FRONTEND_URL}/projects/${proj.id}/runs/test-history`);
    await screenshot(page, 'test-history-overview');

    // Tests tab
    try {
      await page.getByRole('button', { name: 'Tests' }).click({ timeout: 3000 });
      await screenshotState(page, 'test-history-tests');
    } catch (e) {
      console.warn('  ⚠  test-history-tests skipped:', e.message);
    }

    // Flaky tab
    try {
      await page.getByRole('button', { name: 'Flaky' }).click({ timeout: 3000 });
      await screenshotState(page, 'test-history-flaky');
    } catch (e) {
      console.warn('  ⚠  test-history-flaky skipped:', e.message);
    }

    // Compare tab
    try {
      await page.getByRole('button', { name: 'Compare' }).click({ timeout: 3000 });
      await screenshotState(page, 'test-history-compare');
    } catch (e) {
      console.warn('  ⚠  test-history-compare skipped:', e.message);
    }

    // Import TRX modal
    try {
      await page.getByRole('button', { name: 'Import TRX' }).click({ timeout: 3000 });
      await screenshotState(page, 'test-history-import');
      await page.keyboard.press('Escape');
      await page.waitForTimeout(200);
    } catch (e) {
      console.warn('  ⚠  test-history-import skipped:', e.message);
    }

    // --- Project Settings — Custom Properties ---
    await page.goto(`${FRONTEND_URL}/projects/${proj.id}/settings`);
    await page.waitForLoadState('networkidle');
    await screenshot(page, 'project-settings');

    // Scroll to Custom Properties section and capture
    try {
      const heading = page.getByText('Custom Properties').first();
      await heading.waitFor({ timeout: 3000 });
      await heading.scrollIntoViewIfNeeded();
      await screenshotState(page, 'project-settings-custom-properties');
    } catch (e) {
      console.warn('  ⚠  project-settings-custom-properties skipped:', e.message);
    }

    // Custom property add form — default (Text type)
    try {
      await page.getByRole('button', { name: '+ Add Property' }).click({ timeout: 3000 });
      await screenshotState(page, 'custom-property-form');

      // Use a single stable locator for the type select (all options are always in the DOM)
      const typeSelect = page.locator('select').filter({ hasText: 'Text' }).first();

      // Switch to Enum type (shows "Allowed values" field)
      await typeSelect.selectOption({ label: 'Enum (pick list)' });
      await screenshotState(page, 'custom-property-form-enum');

      // Switch to Number type (shows min/max fields)
      await typeSelect.selectOption({ label: 'Number' });
      await screenshotState(page, 'custom-property-form-number');

      // Switch to Date type (shows date range pickers)
      await typeSelect.selectOption({ label: 'Date' });
      await screenshotState(page, 'custom-property-form-date');

      await page.keyboard.press('Escape');
      await page.waitForTimeout(300);
    } catch (e) {
      console.warn('  ⚠  custom-property-form skipped:', e.message);
    }
  }

  // --- Demo component pages ---
  console.log('Taking demo page screenshots…');
  try {
    await page.goto(`${FRONTEND_URL}/demo`);
    await screenshot(page, 'demo-index');

    // Test History chart — failure-rate mode (default)
    await page.goto(`${FRONTEND_URL}/demo/test-history-chart`);
    await screenshot(page, 'project-dashboard-test-history-failure-rate');

    // Switch to pass/fail mode
    try {
      await page.getByText('Pass/Fail').first().click({ timeout: 3000 });
      await screenshotState(page, 'project-dashboard-test-history-pass-fail');
    } catch (e) {
      console.warn('  ⚠  test-history pass/fail switch skipped:', e.message);
    }

    // Switch to groups mode
    try {
      await page.getByText('Groups').first().click({ timeout: 3000 });
      await screenshotState(page, 'project-dashboard-test-history-groups');
    } catch (e) {
      console.warn('  ⚠  test-history groups switch skipped:', e.message);
    }

    // Switch to runs x-mode
    try {
      await page.getByText('Runs').first().click({ timeout: 3000 });
      await screenshotState(page, 'project-dashboard-test-history-runs-mode');
    } catch (e) {
      console.warn('  ⚠  test-history runs mode switch skipped:', e.message);
    }

    // Demo kanban page
    await page.goto(`${FRONTEND_URL}/demo/kanban`);
    await screenshot(page, 'project-dashboard-kanban-card');
  } catch (e) {
    console.warn('  ⚠  Demo pages skipped:', e.message);
  }

  await browser.close();
  console.log('Done.');

  if (CREATE_PR) {
    console.log('Creating GitHub Pull Request…');
    await createPullRequest();
  }
}

/**
 * Create a GitHub Pull Request with the updated screenshots.
 * Encodes each PNG in the output directory as base64, commits them to a new branch,
 * and opens a draft PR. Requires GITHUB_TOKEN with `repo` scope.
 */
async function createPullRequest() {
  if (!GITHUB_TOKEN) throw new Error('GITHUB_TOKEN is required for --create-pr');

  const [owner, repo] = GITHUB_REPO.split('/');
  const apiBase = `https://api.github.com/repos/${owner}/${repo}`;
  const headers = {
    Authorization: `Bearer ${GITHUB_TOKEN}`,
    Accept: 'application/vnd.github+json',
    'X-GitHub-Api-Version': '2022-11-28',
    'Content-Type': 'application/json',
  };

  // Get the SHA of the base branch HEAD
  const baseRef = await fetch(`${apiBase}/git/ref/heads/${PR_BASE}`, { headers });
  if (!baseRef.ok) {
    const err = await baseRef.text();
    throw new Error(`Failed to resolve base branch '${PR_BASE}': ${err}`);
  }
  const baseRefJson = await baseRef.json();
  const baseSha = baseRefJson.object?.sha;
  if (!baseSha) throw new Error(`Could not read SHA for base branch '${PR_BASE}': ${JSON.stringify(baseRefJson)}`);

  // Create the new branch
  const branchRes = await fetch(`${apiBase}/git/refs`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ ref: `refs/heads/${PR_BRANCH}`, sha: baseSha }),
  });
  if (!branchRes.ok) {
    const err = await branchRes.text();
    throw new Error(`Failed to create branch ${PR_BRANCH}: ${err}`);
  }
  console.log(`  Branch created: ${PR_BRANCH}`);

  // Commit each screenshot file
  const screenshots = fs.readdirSync(OUTPUT_DIR).filter(f => f.endsWith('.png'));
  for (const file of screenshots) {
    const filePath = path.join(OUTPUT_DIR, file);
    const content64 = fs.readFileSync(filePath).toString('base64');
    const repoPath = `docs/assets/screenshots/${file}`;

    // Check if file exists to get its current SHA (required for update)
    let fileSha;
    const existing = await fetch(`${apiBase}/contents/${repoPath}?ref=${PR_BRANCH}`, { headers });
    if (existing.ok) {
      const existingJson = await existing.json();
      fileSha = existingJson.sha;
    }

    const body = {
      message: `docs: update screenshot ${file}`,
      content: content64,
      branch: PR_BRANCH,
      ...(fileSha ? { sha: fileSha } : {}),
    };
    const putRes = await fetch(`${apiBase}/contents/${repoPath}`, {
      method: 'PUT',
      headers,
      body: JSON.stringify(body),
    });
    if (!putRes.ok) {
      const err = await putRes.text();
      console.warn(`  ⚠  Failed to commit ${file}: ${err}`);
    } else {
      console.log(`  ✓  Committed ${repoPath}`);
    }
  }

  // Open the PR
  const now = new Date().toISOString().slice(0, 10);
  const prRes = await fetch(`${apiBase}/pulls`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      title: `docs: update screenshots (${now})`,
      body: `Automated screenshot update generated by \`scripts/take-screenshots.js --create-pr\`.\n\nUpdated ${screenshots.length} screenshot(s).`,
      head: PR_BRANCH,
      base: PR_BASE,
      draft: true,
    }),
  });
  const prJson = await prRes.json();
  if (!prRes.ok) throw new Error(`Failed to create PR: ${JSON.stringify(prJson)}`);
  console.log(`  PR created: ${prJson.html_url}`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
