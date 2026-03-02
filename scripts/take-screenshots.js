#!/usr/bin/env node
// @ts-check
/**
 * Playwright screenshot script for IssuePit user documentation.
 *
 * Usage:
 *   FRONTEND_URL=http://localhost:3000 node scripts/take-screenshots.js [output-dir]
 *
 * The script:
 *   1. Registers a fresh user and logs in.
 *   2. Seeds minimal demo data (org, project, issues, agent mode).
 *   3. Takes screenshots of each main UI page.
 *   4. Saves them to <output-dir> (defaults to docs/assets/screenshots).
 */

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const FRONTEND_URL = (process.env.FRONTEND_URL || 'http://localhost:3000').replace(/\/$/, '');
const API_URL = (process.env.API_URL || 'http://localhost:5000').replace(/\/$/, '');
const OUTPUT_DIR = process.argv[2] || path.join(__dirname, '..', 'docs', 'assets', 'screenshots');

const USERNAME = `docs${Math.random().toString(36).slice(2, 10)}`;
const PASSWORD = 'DocsPass1!';

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

async function register(apiClient, username, password) {
  const res = await apiClient.post(`${API_URL}/api/auth/register`, {
    headers: { 'Content-Type': 'application/json' },
    data: JSON.stringify({ username, password }),
  });
  if (!res.ok()) throw new Error(`Register failed: ${res.status()} ${await res.text()}`);
  return res.json();
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
  const issueStatuses = ['open', 'in_progress', 'done'];
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
      dockerImage: 'ghcr.io/sst/opencode:latest',
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

async function main() {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });

  console.log(`Waiting for backend at ${API_URL}…`);
  await waitForBackend();

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
  });

  // --- Auth ---
  console.log(`Registering user ${USERNAME}…`);
  const loginPage = await context.newPage();
  await loginPage.goto(`${FRONTEND_URL}/login`);
  await loginPage.waitForLoadState('networkidle');
  await loginPage.click("button:has-text('Create account')");
  await loginPage.fill("input[autocomplete='username']", USERNAME);
  await loginPage.fill("input[autocomplete='new-password']", PASSWORD);
  await loginPage.click("button[type='submit']");
  await loginPage.waitForURL(`${FRONTEND_URL}/`, { timeout: 20_000 });

  // Get tenant ID from API
  const apiClient = await context.request.newContext({ baseURL: API_URL });
  const meRes = await apiClient.get(`${API_URL}/api/auth/me`);
  const me = await meRes.json();
  const tenantId = me.tenantId || me.tenant?.id;
  if (!tenantId) {
    throw new Error(`Could not determine tenantId from /api/auth/me response: ${JSON.stringify(me)}`);
  }

  await loginPage.close();

  // --- Seed data ---
  console.log('Seeding demo data…');
  await seedData(apiClient, tenantId);

  // --- Screenshots ---
  console.log(`Taking screenshots → ${OUTPUT_DIR}`);
  const page = await context.newPage();

  await page.goto(FRONTEND_URL);
  await screenshot(page, 'dashboard');

  await page.goto(`${FRONTEND_URL}/projects`);
  await screenshot(page, 'projects');

  await page.goto(`${FRONTEND_URL}/issues`);
  await screenshot(page, 'issues');

  // Try to get the first project for board/issues
  const projectsRes = await apiClient.get(`${API_URL}/api/projects`, {
    headers: tenantId ? { 'X-Tenant-Id': tenantId } : {},
  });
  const projects = await projectsRes.json();
  if (Array.isArray(projects) && projects.length > 0) {
    const proj = projects[0];
    await page.goto(`${FRONTEND_URL}/projects/${proj.slug || proj.id}/board`);
    await screenshot(page, 'kanban');
  }

  await page.goto(`${FRONTEND_URL}/agents`);
  await screenshot(page, 'agents');

  await page.goto(`${FRONTEND_URL}/configuration/api-keys`);
  await screenshot(page, 'api-keys');

  await browser.close();
  console.log('Done.');
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
