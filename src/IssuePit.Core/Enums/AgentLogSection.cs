namespace IssuePit.Core.Enums;

/// <summary>
/// Identifies which phase of the agent workflow a log line belongs to.
///
/// The execution flow is currently always a linear chain (no branching). Branching could
/// be added in future (e.g. running agent fixes on separate git branches), but today all
/// phases execute sequentially inside a single session.
///
/// Chain: InitialAgentRun → [UncommittedChangesFix] → CiCdRun(1) → [CiCdFixRun(1)] → CiCdRun(2) → …
/// </summary>
public enum AgentLogSection
{
    /// <summary>The initial opencode agent run for the issue.</summary>
    InitialAgentRun = 1,

    /// <summary>
    /// A follow-up opencode run that commits or .gitignore-s uncommitted
    /// files left behind by the initial run.
    /// </summary>
    UncommittedChangesFix = 2,

    /// <summary>
    /// A CI/CD pipeline run triggered after the agent committed changes.
    /// <see cref="AgentSessionLog.SectionIndex"/> holds the attempt number (1-based).
    /// </summary>
    CiCdRun = 3,

    /// <summary>
    /// An opencode run that addresses CI/CD failures from the preceding CI/CD run.
    /// <see cref="AgentSessionLog.SectionIndex"/> holds the attempt number (1-based).
    /// Future: could fork the agent session onto a new git branch rather than continuing
    /// in the same container; today all fix phases share one container.
    /// </summary>
    CiCdFixRun = 4,
}
