---
title: Documentation Guidelines (Agent Reference)
layout: default
nav_exclude: true
search_exclude: true
---

# IssuePit Documentation Guidelines

> **This file is for coding agents**, not end users. It is intentionally excluded from the documentation navigation. It describes how to design, write, and maintain the `docs/` folder.

---

## Documentation Structure

The `docs/` folder serves as the IssuePit user-facing documentation site (Jekyll / just-the-docs). Keep it focused on what **end users** need to know.

### User Docs vs. Developer Docs

| Type | Location | Audience |
|------|----------|----------|
| User docs | `docs/*.md` (with `nav_order`) | End users — feature descriptions, how-tos, configuration |
| Developer / agent docs | Root `agents.md` | Coding agents — conventions, coding guidelines, testing rules |
| Architecture / internals | `docs/architecture.md`, `docs/developer.md` | Developers who contribute to IssuePit itself |

**Rule:** Never add developer-only content (coding conventions, testing patterns, internal implementation details) to a `docs/` page that has a `nav_order`. Developer content belongs in the root `agents.md` or in `docs/developer.md` / `docs/architecture.md`.

### What Belongs in Each Section

- `docs/agents.md` — user-facing guide: how to create agent modes, queues, MCP servers, container runtimes
- `agents.md` (root) — coding agent guidelines: date formats, DateDisplay component, API response objects, testing conventions, E2E timeouts, PR screenshot rules
- `docs/issuepitAgents.md` (this file) — meta-documentation about how to design the docs

---

## Adding or Updating Pages

1. **User feature page** — add a new `docs/<feature>.md` with Jekyll front matter (title, layout, nav_order).
2. **Developer convention** — add it to the root `agents.md` under an appropriate `##` heading.
3. **Architecture detail** — add to `docs/architecture.md`.

When adding a new page:
- Give it a sensible `nav_order` so it appears in the right place in the sidebar.
- Add a screenshot entry to `scripts/take-screenshots.js` if the page has a matching UI screen.
- Cross-link from related pages where helpful.

---

## Screenshot Script

The `scripts/take-screenshots.js` script automates documentation screenshots. It:

1. Starts a Playwright browser session.
2. Logs in with the pre-seeded `alice`/`alice` account (set by Aspire Migrator).
3. Navigates to each page and saves a screenshot to `docs/assets/screenshots/`.

### Adding a New Screenshot

Find the `pages` array in `scripts/take-screenshots.js` and add an entry:

```js
{ name: 'my-feature', path: '/my-feature', waitFor: 'text=My Feature Heading' },
```

### Checking Screenshots

Before merging a PR that modifies `scripts/take-screenshots.js`:
- Run the script locally and verify that each screenshot shows the **actual UI after login** (not a blank page, login screen, or wrong page).
- Check that screenshot filenames match what is referenced in the docs markdown (`{{ '/assets/screenshots/<name>.png' | relative_url }}`).

### Running Locally

```sh
# Start the full Aspire stack first, then:
FRONTEND_URL=http://localhost:3000 \
API_URL=http://localhost:5000 \
SCREENSHOT_USERNAME=alice \
SCREENSHOT_PASSWORD=alice \
node scripts/take-screenshots.js /tmp/screenshots
```

---

## Splitting Dev Content Out of User Docs

If you find developer-only content (coding conventions, internal architecture notes, testing patterns) inside a user-facing `docs/` page:

1. Move it to `agents.md` (root) if it is a coding convention or rule for coding agents.
2. Move it to `docs/developer.md` or `docs/architecture.md` if it describes how IssuePit works internally.
3. Remove or replace the section in the user-facing page with a short cross-reference if needed.

**Example pattern to avoid:**

```md
<!-- docs/agents.md — BAD: mixing user and dev content -->
## Tips for Writing System Prompts   ← user content ✅
## Coding Agent Guidelines           ← dev content ❌ (belongs in agents.md)
```
