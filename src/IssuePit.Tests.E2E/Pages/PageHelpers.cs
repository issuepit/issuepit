using Microsoft.Playwright;

namespace IssuePit.Tests.E2E.Pages;

/// <summary>
/// Shared Playwright helper utilities used by all page objects.
/// </summary>
internal static class PageHelpers
{
    /// <summary>
    /// CSS selector for the generic page-ready attribute set by the <c>usePageReady</c> Vue
    /// composable on <c>&lt;body&gt;</c> once Vue has hydrated the page component and the
    /// page's initial data fetch has completed.
    /// </summary>
    private const string PageReadySelector = "body[data-page-ready='true']";

    /// <summary>
    /// Waits for the generic page-ready signal on the current page.
    /// Use after <c>page.GotoAsync</c> to ensure Vue has fully hydrated and the initial
    /// data load has completed before interacting with the page.
    /// </summary>
    /// <param name="page">The Playwright page instance.</param>
    /// <param name="timeout">
    /// Maximum wait in milliseconds; defaults to <see cref="E2ETimeouts.NavigationLong"/>.
    /// Use the longer timeout on cold CI starts where Nuxt dev-server must compile pages on demand.
    /// </param>
    public static Task WaitForPageReadyAsync(this IPage page, float timeout = E2ETimeouts.NavigationLong)
        => page.WaitForSelectorAsync(PageReadySelector, new PageWaitForSelectorOptions { Timeout = timeout });
}
