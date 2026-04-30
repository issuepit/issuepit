/**
 * Signals to Playwright E2E tests that the current page is fully hydrated and its initial
 * data load has completed.  Call once per page component, passing the critical async init work.
 *
 * Sets `data-page-ready="true"` on `<body>` in onMounted() after the init function resolves
 * (or rejects — readiness is declared either way so tests are not blocked by transient errors).
 * Clears the attribute in onUnmounted() so navigating away resets the signal for the next page.
 *
 * Playwright selector: `body[data-page-ready='true']`
 *
 * @example
 * // hydration-only (no async data):
 * usePageReady()
 *
 * @example
 * // hydration + initial data fetch:
 * usePageReady(() => store.fetchIssues(id))
 */
export function usePageReady(initFn?: () => Promise<unknown> | unknown): void {
  if (import.meta.server) return

  onMounted(async () => {
    // Clear any stale attribute from a previous page visit before starting so the new
    // page's ready signal is not prematurely inherited by this navigation.
    document.body.removeAttribute('data-page-ready')
    try {
      if (initFn) await initFn()
    } finally {
      document.body.setAttribute('data-page-ready', 'true')
    }
  })

  onUnmounted(() => {
    document.body.removeAttribute('data-page-ready')
  })
}
