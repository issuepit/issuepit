using System.Text.Json.Serialization;

namespace IssuePit.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<IssueStatus>))]
public enum IssueStatus
{
    [JsonStringEnumMemberName("backlog")]
    Backlog = 0,
    [JsonStringEnumMemberName("todo")]
    Todo = 1,
    [JsonStringEnumMemberName("in_progress")]
    InProgress = 2,
    [JsonStringEnumMemberName("in_review")]
    InReview = 3,
    [JsonStringEnumMemberName("done")]
    Done = 4,
    [JsonStringEnumMemberName("cancelled")]
    Cancelled = 5,
    [JsonStringEnumMemberName("ready_to_merge")]
    ReadyToMerge = 6
}
