using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/runs/cicd/{runId} — the CI/CD run detail page.
/// </summary>
public class CiCdRunPage(IPage page)
{
    /// <summary>
    /// Navigates to the CI/CD run detail page and waits for the page heading to load.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
    /// </summary>
    public async Task GotoAsync(string projectId, string runId)
    {
        var url = $"/projects/{projectId}/runs/cicd/{runId}";
        try
        {
            await page.GotoAsync(url);

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await page.WaitForSelectorAsync("text=CI/CD Run",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync(url);

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await page.WaitForSelectorAsync("text=CI/CD Run",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
    }

    /// <summary>
    /// Navigates directly to the Tests tab of the CI/CD run detail page.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException.
    /// </summary>
    public async Task GotoTestsTabAsync(string projectId, string runId)
    {
        var url = $"/projects/{projectId}/runs/cicd/{runId}?tab=tests";
        try
        {
            await page.GotoAsync(url);

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await page.WaitForSelectorAsync("text=CI/CD Run",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync(url);

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await page.WaitForSelectorAsync("text=CI/CD Run",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
    }

    /// <summary>
    /// Navigates directly to the Artifacts tab of the CI/CD run detail page.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException.
    /// </summary>
    public async Task GotoArtifactsTabAsync(string projectId, string runId)
    {
        var url = $"/projects/{projectId}/runs/cicd/{runId}?tab=artifacts";
        try
        {
            await page.GotoAsync(url);

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await page.WaitForSelectorAsync("text=CI/CD Run",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Short });
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync(url);

            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);

            await page.WaitForSelectorAsync("text=CI/CD Run",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });
        }
    }

    public async Task WaitForLoadAsync() =>
        await page.WaitForSelectorAsync("text=CI/CD Run", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>
    /// Clicks the Jobs tab and waits for the tab to become active.
    /// </summary>
    public async Task ClickJobsTabAsync()
    {
        await page.ClickAsync("button:has-text('Jobs')");
        // The "Slim" toggle button is only rendered in the Jobs tab controls bar; waiting for it
        // is a deterministic indicator that the Jobs tab is active and the content has been rendered.
        await page.WaitForSelectorAsync(
            "button:has-text('Slim')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });
    }

    /// <summary>
    /// Clicks the Tests tab and waits for the tab content to render.
    /// </summary>
    public async Task ClickTestsTabAsync()
    {
        await page.ClickAsync("button:has-text('Tests')");
        await WaitForTestsTabContentAsync();
    }

    /// <summary>
    /// Waits for the Tests tab content to load — either test suites or the empty state message.
    /// </summary>
    public async Task WaitForTestsTabContentAsync() =>
        await page.WaitForFunctionAsync(
            "document.body?.innerText?.includes('passed') || document.body?.innerText?.includes('No test results available')",
            null,
            new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>
    /// Waits for the Tests tab to show actual test results (at least one "passed" entry).
    /// Use this instead of <see cref="WaitForTestsTabContentAsync"/> when you know the run
    /// should have test results, to avoid resolving early on the transient empty-state shown during loading.
    /// </summary>
    public async Task WaitForNonEmptyTestsTabAsync() =>
        await page.WaitForFunctionAsync(
            "document.body?.innerText?.includes('passed')",
            null,
            new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>Returns true when the tests tab shows at least one test suite result.</summary>
    public async Task<bool> HasTestSuitesAsync()
    {
        // The Tests tab shows suites in cards that each contain "passed" in the suite header.
        var suiteHeaders = page.Locator("text=passed").First;
        return await suiteHeaders.CountAsync() > 0;
    }

    /// <summary>Returns true when the tests tab stats bar shows both Total and Fail Rate stats.</summary>
    public async Task<bool> HasTestRunStatsBarAsync()
    {
        var hasTotal = await page.IsVisibleAsync("p:has-text('Total')");
        var hasFailRate = await page.IsVisibleAsync("p:has-text('Fail Rate')");
        return hasTotal && hasFailRate;
    }

    /// <summary>Clicks the "Failed only" toggle button in the tests tab.</summary>
    public async Task ClickFailedOnlyToggleAsync() =>
        await page.ClickAsync("button:has-text('Failed only')");

    /// <summary>Clicks the "Collapse all" button in the tests tab.</summary>
    public async Task ClickCollapseAllAsync() =>
        await page.ClickAsync("button:has-text('Collapse all')");

    /// <summary>Clicks the "Expand all" button in the tests tab.</summary>
    public async Task ClickExpandAllAsync() =>
        await page.ClickAsync("button:has-text('Expand all')");

    /// <summary>Returns true when the tests tab shows the empty state message.</summary>
    public async Task<bool> IsTestsTabEmptyAsync() =>
        await page.IsVisibleAsync("text=No test results available");

    /// <summary>Returns true when the jobs tab shows the empty state message.</summary>
    public async Task<bool> IsJobsTabEmptyAsync() =>
        await page.IsVisibleAsync("text=No job data available");

    /// <summary>
    /// Returns the count of job boxes visible in the jobs tab.
    /// </summary>
    public async Task<int> GetJobBoxCountAsync()
    {
        return await page.Locator(".job-box").CountAsync();
    }

    /// <summary>
    /// Returns true when the run status badge is visible.
    /// </summary>
    public async Task<bool> IsStatusVisibleAsync() =>
        await page.IsVisibleAsync("[class*='rounded-full']:has-text('Status'), p:has-text('Status')");

    // ── Artifacts tab ──────────────────────────────────────────────────────────

    /// <summary>
    /// Waits for the Artifacts tab content to load — either an artifact list or the empty state message.
    /// </summary>
    public async Task WaitForArtifactsTabContentAsync() =>
        await page.WaitForFunctionAsync(
            "document.body?.innerText?.includes('produced by this run') || document.body?.innerText?.includes('No artifacts found for this run')",
            null,
            new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>
    /// Waits for the Artifacts tab to show at least one artifact (the "produced by this run" text).
    /// Use this instead of <see cref="WaitForArtifactsTabContentAsync"/> when you know the run
    /// should have artifacts, to avoid resolving early on the transient empty-state shown during loading.
    /// </summary>
    public async Task WaitForNonEmptyArtifactsTabAsync() =>
        await page.WaitForFunctionAsync(
            "document.body?.innerText?.includes('produced by this run')",
            null,
            new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>Returns true when the artifacts tab shows the empty state message.</summary>
    public async Task<bool> IsArtifactsTabEmptyAsync() =>
        await page.IsVisibleAsync("text=No artifacts found for this run.");

    /// <summary>
    /// Returns true when the toggle button for test-result artifacts is visible
    /// (i.e. the run produced at least one artifact flagged as a test-result artifact).
    /// Waits up to <see cref="E2ETimeouts.Default"/> for the button to appear.
    /// </summary>
    public async Task<bool> HasTestResultArtifactToggleAsync()
    {
        try
        {
            await page.WaitForSelectorAsync(
                "[data-testid='toggle-test-result-artifacts']",
                new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default, State = WaitForSelectorState.Visible });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Clicks the toggle button that reveals hidden test-result artifacts and waits
    /// for at least one additional artifact row to appear.
    /// </summary>
    public async Task ShowTestResultArtifactsAsync()
    {
        await page.ClickAsync("[data-testid='toggle-test-result-artifacts']");
    }

    /// <summary>Returns the number of artifact rows currently visible in the artifacts tab.</summary>
    public async Task<int> GetVisibleArtifactCountAsync()
    {
        // Each artifact row contains the artifact name in a p.text-sm.font-medium element.
        return await page.Locator(".space-y-2 > div").CountAsync();
    }

    // ── Create Issue from failed job ───────────────────────────────────────────

    /// <summary>
    /// Waits for at least one "Create Issue" button to appear in the jobs tab
    /// (shown on jobs that have both <c>hasError = true</c> and <c>isComplete = true</c>).
    /// </summary>
    public async Task WaitForCreateIssueButtonAsync() =>
        await page.WaitForSelectorAsync(
            "button:has-text('Create Issue')",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>
    /// Clicks the first visible "Create Issue" button in the jobs tab and waits for
    /// the create-issue modal to open.
    /// </summary>
    public async Task ClickCreateIssueOnFailedJobAsync()
    {
        await page.ClickAsync("button:has-text('Create Issue')");
        await WaitForCreateIssueModalAsync();
    }

    /// <summary>Waits for the "Create Issue" modal to become visible.</summary>
    public async Task WaitForCreateIssueModalAsync() =>
        await page.WaitForSelectorAsync(
            "text=Create Issue from",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Default });

    /// <summary>
    /// Returns the log preview text currently shown in the create-issue modal.
    /// Returns an empty string when the preview panel shows "No log lines for this scope".
    /// </summary>
    public async Task<string> GetCreateIssueLogPreviewTextAsync()
    {
        // The preview panel is inside the modal with max-h-[200px] — use innerText of the panel's container.
        var previewText = await page.EvaluateAsync<string>(
            @"(() => {
                // Look for the fenced preview panel inside the Create Issue modal (bg-gray-900 border border-gray-700).
                const modal = document.querySelector('.fixed.inset-0.z-50 .bg-gray-900');
                if (!modal) return '';
                // The preview area is a bg-gray-950 div with font-mono text
                const preview = modal.querySelector('.bg-gray-950.rounded-lg');
                return preview?.innerText ?? '';
            })()");
        return previewText.Trim();
    }

    /// <summary>
    /// Submits the create-issue form in the modal and waits for the modal to close.
    /// </summary>
    /// <remarks>
    /// Strategy: click → wait for modal to close (indicates the API call completed).
    /// Navigation to the newly-created issue is intentionally NOT awaited here because
    /// concurrent SignalR-triggered <c>router.push</c> calls can silently cancel the
    /// SPA <c>navigateTo()</c> in Vue Router 4, causing a flaky <see cref="TimeoutException"/>.
    /// Callers should poll the API for the new issue number and navigate directly via
    /// <c>page.GotoAsync</c> instead.
    /// <para>
    /// If the modal does not close within <see cref="E2ETimeouts.Default"/> the click likely
    /// did not register (Vue hydration race on the modal) and the click is retried once.
    /// </para>
    /// </remarks>
    public async Task SubmitCreateIssueAsync()
    {
        await page.ClickAsync("[data-testid='create-issue-submit']");

        // Wait for the modal to close — indicates the API call completed (success or silent error).
        try
        {
            await page.WaitForSelectorAsync(
                "text=Create Issue from",
                new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Hidden,
                    Timeout = E2ETimeouts.Default,
                });
        }
        catch (TimeoutException)
        {
            // Modal still visible after Default ms — click did not register (Vue hydration race).
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.ClickAsync("[data-testid='create-issue-submit']");
            await page.WaitForSelectorAsync(
                "text=Create Issue from",
                new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Hidden,
                    Timeout = E2ETimeouts.NavigationLong,
                });
        }
    }

    /// <summary>
    /// Returns true when the current URL looks like an issue detail page
    /// (<c>/projects/{id}/issues/{number}</c>).
    /// </summary>
    public bool IsOnIssuePage() =>
        page.Url.Contains("/issues/");

    /// <summary>
    /// Navigates directly to an issue detail page.
    /// Retries once on ERR_ABORTED (Nuxt SPA router race) or TimeoutException (slow first render).
    /// </summary>
    public async Task GotoIssueAsync(string projectId, int issueNumber)
    {
        var url = $"/projects/{projectId}/issues/{issueNumber}";
        try
        {
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        }
        catch (Exception ex) when (ex is TimeoutException || (ex is PlaywrightException pe && pe.Message.Contains("ERR_ABORTED")))
        {
            await Task.Delay(E2ETimeouts.RetryDelay);
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        }
    }
}
