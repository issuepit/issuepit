using System.Text.Json.Serialization;

namespace IssuePit.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<IssueType>))]
public enum IssueType
{
    [JsonStringEnumMemberName("issue")]
    Issue,
    [JsonStringEnumMemberName("bug")]
    Bug,
    [JsonStringEnumMemberName("feature")]
    Feature,
    [JsonStringEnumMemberName("task")]
    Task,
    [JsonStringEnumMemberName("epic")]
    Epic
}
