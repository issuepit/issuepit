using System.Text.Json;

namespace IssuePit.McpServer.Tools;

internal static class ToolSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(object? value) => JsonSerializer.Serialize(value, Options);
}
