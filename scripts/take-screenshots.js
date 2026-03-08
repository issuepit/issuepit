#!/usr/bin/env node
// @ts-check
/**
 * Playwright screenshot script for IssuePit user documentation.
 *
 * Usage:
 *   FRONTEND_URL=http://localhost:3000 node scripts/take-screenshots.js [output-dir]
 *
 * The script:
 *   1. Logs in (or registers) a user.
 *   2. Seeds minimal demo data when no pre-seeded user is configured.
 *   3. Takes screenshots of each main UI page.
 *   4. Saves them to <output-dir> (defaults to docs/assets/screenshots).
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

  // Get the first available project for the kanban screenshot
  const projectsRes = await apiClient.get(`${API_URL}/api/projects`, {
    headers: tenantId ? { 'X-Tenant-Id': tenantId } : {},
  });
  const projects = await projectsRes.json();
  if (Array.isArray(projects) && projects.length > 0) {
    const proj = projects[0];
    await page.goto(`${FRONTEND_URL}/projects/${proj.id}/kanban`);
    await screenshot(page, 'kanban');
  }

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
    await page.goto(`${FRONTEND_URL}/projects/${proj.id}/ci-cd`);
    await screenshot(page, 'cicd');

    await page.goto(`${FRONTEND_URL}/projects/${proj.id}/milestones`);
    await screenshot(page, 'milestones');
  }

  await browser.close();
  console.log('Done.');
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
