namespace IssuePit.Tests.E2E;

/// <summary>
/// Centralised Playwright timeout values (milliseconds) used across all E2E tests and page objects.
/// Using named constants avoids magic numbers, makes intent clear, and allows global tuning from one place.
/// </summary>
/// <remarks>
/// Guidelines for choosing a timeout:
/// <list type="bullet">
///   <item><see cref="Short"/>         – first-attempt / hydration-retry check; a second attempt with the default will follow on failure.</item>
///   <item><see cref="Default"/>       – general element presence wait; also the value passed to <c>SetDefaultTimeout</c>.</item>
///   <item><see cref="Navigation"/>    – full-page navigations such as post-login redirects or initial project-page loads.</item>
///   <item><see cref="NavigationLong"/>– slower cross-page navigations (e.g. navigating into an org or agent detail page).</item>
///   <item><see cref="RetryDelay"/>    – <c>Task.Delay</c> between a failed first navigation attempt and the retry (not a Playwright timeout).</item>
/// </list>
/// </remarks>
internal static class E2ETimeouts
{
    /// <summary>
    /// Short wait used for the first attempt of an action that may need a retry
    /// (Vue SSR hydration race) or for brief UI-feedback checks (modal appeared, etc.).
    /// </summary>
    public const int Short = 5_000;

    /// <summary>
    /// General-purpose element selector / interaction wait and the value passed to
    /// <c>context.SetDefaultTimeout</c> in most test contexts.
    /// </summary>
    public const int Default = 10_000;

    /// <summary>
    /// Navigation timeout for full-page navigations, such as the post-login redirect
    /// to the dashboard or initial project-page loads.
    /// </summary>
    public const int Navigation = 15_000;

    /// <summary>
    /// Extended navigation timeout for slower cross-page navigations
    /// (e.g. navigating into an org or agent detail page using <c>WaitUntilState.Commit</c>).
    /// </summary>
    public const int NavigationLong = 20_000;

    /// <summary>
    /// Delay (milliseconds) between a failed first navigation attempt and the retry.
    /// Passed to <c>Task.Delay</c>, not to a Playwright API.
    /// </summary>
    public const int RetryDelay = 1_500;

    /// <summary>
    /// Polling deadline (milliseconds) when waiting for a specific log line to appear
    /// in a backend session log API response. The session status may be set to Running
    /// before all debug log lines are persisted, so tests must poll rather than fetch once.
    /// Passed to <c>DateTime.UtcNow.AddMilliseconds</c>, not to a Playwright API.
    /// </summary>
    public const int LogPollTimeoutMs = 30_000;

    /// <summary>
    /// Delay (milliseconds) between successive poll attempts when waiting for a log line.
    /// Passed to <c>Task.Delay</c>, not to a Playwright API.
    /// </summary>
    public const int LogPollDelayMs = 500;
}
