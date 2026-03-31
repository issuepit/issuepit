namespace IssuePit.Core.Enums;

/// <summary>
/// Controls how the silent flag is applied on a per-notification basis.
/// </summary>
public enum TelegramSilentMode
{
    /// <summary>Use the global IsSilent flag on the chat/bot.</summary>
    None = 0,

    /// <summary>Only the first notification of each type makes a sound; subsequent ones are silent.</summary>
    SilentAfterFirst = 1,

    /// <summary>Notifications become silent once the rate limit is hit.</summary>
    SilentAfterRateLimit = 2,

    /// <summary>All notifications are always sent silently.</summary>
    AlwaysSilent = 3,
}
