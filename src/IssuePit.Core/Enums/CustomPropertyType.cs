using System.Text.Json.Serialization;

namespace IssuePit.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<CustomPropertyType>))]
public enum CustomPropertyType
{
    [JsonStringEnumMemberName("text")]
    Text = 0,
    [JsonStringEnumMemberName("enum")]
    Enum = 1,
    [JsonStringEnumMemberName("number")]
    Number = 2,
    [JsonStringEnumMemberName("date")]
    Date = 3,
    [JsonStringEnumMemberName("person")]
    Person = 4,
    [JsonStringEnumMemberName("agent")]
    Agent = 5,
    [JsonStringEnumMemberName("bool")]
    Bool = 6,
}
