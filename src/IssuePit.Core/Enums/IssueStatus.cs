using System.Text.Json.Serialization;

namespace IssuePit.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<IssueStatus>))]
public enum IssueStatus
{
    [JsonStringEnumMemberName("backlog")]
    Backlog,
    [JsonStringEnumMemberName("todo")]
    Todo,
    [JsonStringEnumMemberName("in_progress")]
    InProgress,
    [JsonStringEnumMemberName("in_review")]
    InReview,
    [JsonStringEnumMemberName("done")]
    Done,
    [JsonStringEnumMemberName("cancelled")]
    Cancelled
}
