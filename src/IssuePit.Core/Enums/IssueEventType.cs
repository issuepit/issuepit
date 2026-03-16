using System.Text.Json.Serialization;

namespace IssuePit.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<IssueEventType>))]
public enum IssueEventType
{
    [JsonStringEnumMemberName("created")]
    Created,
    [JsonStringEnumMemberName("title_changed")]
    TitleChanged,
    [JsonStringEnumMemberName("description_changed")]
    DescriptionChanged,
    [JsonStringEnumMemberName("status_changed")]
    StatusChanged,
    [JsonStringEnumMemberName("priority_changed")]
    PriorityChanged,
    [JsonStringEnumMemberName("type_changed")]
    TypeChanged,
    [JsonStringEnumMemberName("label_added")]
    LabelAdded,
    [JsonStringEnumMemberName("label_removed")]
    LabelRemoved,
    [JsonStringEnumMemberName("assignee_added")]
    AssigneeAdded,
    [JsonStringEnumMemberName("assignee_removed")]
    AssigneeRemoved,
    [JsonStringEnumMemberName("milestone_set")]
    MilestoneSet,
    [JsonStringEnumMemberName("milestone_cleared")]
    MilestoneCleared,
    [JsonStringEnumMemberName("property_changed")]
    PropertyChanged,
}
