using System.Text.Json.Serialization;

namespace IssuePit.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<IssuePriority>))]
public enum IssuePriority
{
    [JsonStringEnumMemberName("no_priority")]
    NoPriority,
    [JsonStringEnumMemberName("urgent")]
    Urgent,
    [JsonStringEnumMemberName("high")]
    High,
    [JsonStringEnumMemberName("medium")]
    Medium,
    [JsonStringEnumMemberName("low")]
    Low,
    // VeryHigh and Unknown are appended at the end to preserve integer DB values for existing entries
    [JsonStringEnumMemberName("very_high")]
    VeryHigh,
    [JsonStringEnumMemberName("unknown")]
    Unknown
}
