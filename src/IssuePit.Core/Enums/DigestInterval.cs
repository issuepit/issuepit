namespace IssuePit.Core.Enums;

/// <summary>
/// Controls how frequently a bot sends aggregated notification digests.
/// <see cref="Immediate"/> sends each notification as it occurs;
/// <see cref="Hourly"/> and <see cref="Daily"/> batch notifications for periodic delivery.
/// </summary>
public enum DigestInterval
{
    Immediate = 0,
    Hourly = 1,
    Daily = 2,
}
