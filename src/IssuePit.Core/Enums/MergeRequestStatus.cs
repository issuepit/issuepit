using System.Text.Json.Serialization;

namespace IssuePit.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<MergeRequestStatus>))]
public enum MergeRequestStatus
{
    [JsonStringEnumMemberName("open")]
    Open,
    [JsonStringEnumMemberName("merged")]
    Merged,
    [JsonStringEnumMemberName("closed")]
    Closed,
}
