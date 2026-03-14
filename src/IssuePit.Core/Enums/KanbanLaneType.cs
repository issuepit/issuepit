using System.Text.Json.Serialization;

namespace IssuePit.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<KanbanLaneType>))]
public enum KanbanLaneType
{
    [JsonStringEnumMemberName("status")]
    Status = 0,
    [JsonStringEnumMemberName("label")]
    Label = 1,
    [JsonStringEnumMemberName("type")]
    Type = 2,
    [JsonStringEnumMemberName("agent")]
    Agent = 3,
    [JsonStringEnumMemberName("milestone")]
    Milestone = 4,
}
