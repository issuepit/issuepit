namespace IssuePit.Core.Enums;

/// <summary>
/// Controls the opencode agent type for a nested (child) agent.
/// Maps directly to the <c>mode</c> property in opencode's <c>agent</c> config section.
/// See https://opencode.ai/docs/agents for details.
/// </summary>
public enum OpenCodeAgentType
{
    /// <summary>
    /// A subagent is a specialized assistant that primary agents can invoke for specific tasks.
    /// Subagents can also be manually invoked by @ mentioning them.
    /// </summary>
    SubAgent = 0,

    /// <summary>
    /// A primary agent is the main assistant the user interacts with directly.
    /// Primary agents can be cycled through with the Tab key.
    /// </summary>
    Primary = 1,
}
