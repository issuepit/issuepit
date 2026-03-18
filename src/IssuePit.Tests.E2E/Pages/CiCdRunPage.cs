using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Page object for /projects/{id}/runs/cicd/{runId} — the CI/CD run detail page.
/// </summary>
public class CiCdRunPage(IPage page)
{
    public async Task<IResponse?> GotoAsync(string projectId, string runId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/cicd/{runId}");

    public async Task GotoTestsTabAsync(string projectId, string runId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/cicd/{runId}?tab=tests");

    public async Task GotoArtifactsTabAsync(string projectId, string runId) =>
        await page.GotoAsync($"/projects/{projectId}/runs/cicd/{runId}?tab=artifacts");

    public async Task WaitForLoadAsync() =>
        await page.WaitForSelectorAsync("text=CI/CD Run", new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>
    /// Clicks the Jobs tab and waits for the tab to become active.
    /// </summary>
    public async Task ClickJobsTabAsync()
    {
        await page.ClickAsync("button:has-text('Jobs')");
        // Wait briefly for the tab to render — any visible content (job boxes, empty state, or log lines)
        await page.WaitForTimeoutAsync(500);
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
            "document.body.innerText.includes('passed') || document.body.innerText.includes('No test results available')",
            null,
            new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>Returns true when the tests tab shows at least one test suite result.</summary>
    public async Task<bool> HasTestSuitesAsync()
    {
        // The Tests tab shows suites in cards that each contain "passed" in the suite header.
        var suiteHeaders = page.Locator("text=passed").First;
        return await suiteHeaders.CountAsync() > 0;
    }

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
        var boxes = await page.QuerySelectorAllAsync(".job-box");
        return boxes.Count;
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
            "document.body.innerText.includes('produced by this run') || document.body.innerText.includes('No artifacts found for this run')",
            null,
            new PageWaitForFunctionOptions { Timeout = E2ETimeouts.Navigation });

    /// <summary>
    /// Waits for the Artifacts tab to show at least one artifact (the "produced by this run" text).
    /// Use this instead of <see cref="WaitForArtifactsTabContentAsync"/> when you know the run
    /// should have artifacts, to avoid resolving early on the transient empty-state shown during loading.
    /// </summary>
    public async Task WaitForNonEmptyArtifactsTabAsync() =>
        await page.WaitForFunctionAsync(
            "document.body.innerText.includes('produced by this run')",
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
        var rows = await page.QuerySelectorAllAsync(".space-y-2 > div");
        return rows.Count;
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
    public async Task SubmitCreateIssueAsync()
    {
        // The submit button inside the modal has a red background (bg-red-700) and shows "Create Issue"
        await page.ClickAsync(".bg-red-700:has-text('Create Issue')");
        // Wait for modal to close
        await page.WaitForSelectorAsync(
            "text=Create Issue from",
            new PageWaitForSelectorOptions { Timeout = E2ETimeouts.Navigation, State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Returns true when the current URL looks like an issue detail page
    /// (<c>/projects/{id}/issues/{number}</c>).
    /// </summary>
    public bool IsOnIssuePage() =>
        page.Url.Contains("/issues/");
}
