namespace IssuePit.Core.Enums;

public enum GitRepoStatus
{
    /// <summary>Repository is actively polled.</summary>
    Active = 0,

    /// <summary>Repository has been disabled due to a non-recoverable error (e.g. auth failure, 404).</summary>
    Disabled = 1,

    /// <summary>Repository is temporarily throttled due to a recoverable error (e.g. server-side 5xx).</summary>
    Throttled = 2,
}
