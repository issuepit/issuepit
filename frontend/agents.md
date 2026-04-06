
# UI Conventions

## Date Formats

Always use **ISO 8601 format** (`YYYY-MM-DD`) for dates in custom issue properties, API responses, and any user-visible date fields. Do not rely on browser locale formatting (e.g. `mm/dd/yyyy`) for date values stored or displayed in the application. Date inputs in forms should accept and display dates in `YYYY-MM-DD` format.

For **displaying** dates and times in the Vue frontend, always use the shared `<DateDisplay>` component (`frontend/components/DateDisplay.vue`) instead of inline `toLocaleString`/`toLocaleDateString` calls:

```vue
<!-- Absolute date, European format: "16. Jan 2025" -->
<DateDisplay :date="item.createdAt" mode="absolute" resolution="date" />

<!-- Absolute datetime, 24h clock: "16. Jan 2025, 14:30" -->
<DateDisplay :date="item.startedAt" mode="absolute" resolution="datetime" />

<!-- Relative: "3 minutes ago", "2 hours ago", "yesterday" (tooltip shows full datetime) -->
<DateDisplay :date="item.updatedAt" mode="relative" />

<!-- Auto: relative for recent dates (<7d), absolute beyond that -->
<DateDisplay :date="item.startedAt" mode="auto" />
```

Key formatting rules enforced by `<DateDisplay>`:
- **24-hour clock** — never use AM/PM
- **European day-first format** — `16. Jan 2025` not `Jan 16, 2025`
- Relative labels: `just now`, `X minutes ago`, `X hours ago`, `yesterday`, `X days ago`


## Delete Operations Must Show a Confirm Modal

**All destructive delete operations in the UI must show a confirmation modal** before executing.
Never call a delete API directly from a button click without first showing a modal that requires the user to confirm.

This applies to deleting: issues, attachments, agents, runtimes, MCP servers, API keys, labels, milestones, and any other entity.

The confirmation modal must:
- Clearly state what is being deleted (include the item name where possible).
- Warn that the action cannot be undone.
- Provide a prominent red **Delete** button and a neutral **Cancel** button.

## Searchable Multi-Select Inputs

**Use the `<MultiSelect>` component** (`frontend/components/MultiSelect.vue`) for any filter or selection field where:
- the user may want to select **multiple values**, or
- the list of options is long enough to benefit from a **search/filter input** (e.g. branches, agents, usernames, labels, statuses).

Never use a plain `<input type="text">` for a filter that maps to a discrete set of options. Examples that must use `<MultiSelect>`:
- Branch filters (test history, run lists, CI/CD views)
- Agent assignment filters
- Username / member filters
- Label and status filters

```vue
<MultiSelect
  v-model="selectedBranches"
  :options="branchOptions"
  placeholder="All Branches"
/>
```

Where `options` is `MultiSelectOption[]` (`{ value: string, label: string, dotClass?: string }`).
Populate `options` from the appropriate API endpoint so the user sees real values.
The component handles search, keyboard navigation, checkbox selection, and outside-click dismissal.

