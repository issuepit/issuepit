using System.Text.Json.Serialization;

namespace IssuePit.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<MilestoneStatus>))]
public enum MilestoneStatus
{
    [JsonStringEnumMemberName("open")]
    Open,
    [JsonStringEnumMemberName("closed")]
    Closed
}
