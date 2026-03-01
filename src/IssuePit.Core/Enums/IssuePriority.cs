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
    Low
}
