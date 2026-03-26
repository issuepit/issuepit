using System.Text.Json;

namespace IssuePit.Core.Runners;

/// <summary>
/// Parses newline-delimited JSON event lines emitted by <c>opencode run --format json</c>
/// and converts them to human-readable display text.
///
/// opencode emits one JSON object per line. The event types handled are:
/// <list type="bullet">
///   <item><c>text</c> — assistant text output; extracted and displayed as plain text.</item>
///   <item><c>tool</c> — tool invocation (bash, read, write, etc.); formatted as a summary line.</item>
///   <item><c>session</c> — session state, including cost and token counts; formatted as a stats line
///     with the special <see cref="StatsPrefix"/> marker so the frontend can render it as a UI card.</item>
/// </list>
/// Any line that is not valid JSON, or has an unrecognised event type, is returned unchanged as-is.
///
/// References:
/// <list type="bullet">
///   <item>https://opencode.ai/docs/cli/#run-1</item>
///   <item>https://takopi.dev/reference/runners/opencode/stream-json-cheatsheet/</item>
/// </list>
/// </summary>
public static class OpenCodeJsonLogParser
{
    /// <summary>
    /// Prefix that identifies a formatted opencode stats line stored in the database.
    /// The frontend detects this prefix and renders a stats card instead of plain text.
    /// </summary>
    public const string StatsPrefix = "[opencode:stats] ";

    /// <summary>
    /// Tries to parse <paramref name="rawLine"/> as an opencode JSON event and returns a
    /// human-readable display string. Returns the original line unchanged when it is not
    /// valid JSON or has an unrecognised event type.
    /// </summary>
    public static string ParseLine(string rawLine)
    {
        if (rawLine.Length == 0 || rawLine[0] != '{')
            return rawLine;

        try
        {
            using var doc = JsonDocument.Parse(rawLine);
            var root = doc.RootElement;

            var type = GetString(root, "type");

            return type switch
            {
                "text" => ParseTextEvent(root),
                "tool" => ParseToolEvent(root),
                "session" => ParseSessionEvent(root),
                // Unrecognised type — fall back to raw line.
                _ => rawLine,
            };
        }
        catch (JsonException)
        {
            // Not valid JSON — return as-is.
            return rawLine;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Event parsers
    // ──────────────────────────────────────────────────────────────────────────

    private static string ParseTextEvent(JsonElement root)
    {
        // opencode uses either a nested "properties" object or flat fields.
        var text = TryGetNestedString(root, "properties", "text")
            ?? GetString(root, "text");

        return string.IsNullOrEmpty(text) ? string.Empty : text;
    }

    private static string ParseToolEvent(JsonElement root)
    {
        // Tool info can be inside "properties" ({"type":"tool","properties":{...}}) or flat.
        JsonElement props = root;
        if (root.TryGetProperty("properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
            props = propsEl;

        // "tool" may be a nested object inside "properties", or fields may be at root.
        if (props.TryGetProperty("tool", out var toolEl) && toolEl.ValueKind == JsonValueKind.Object)
            props = toolEl;

        var name = GetString(props, "name") ?? "unknown";
        var state = GetString(props, "state");

        // Only emit the tool output line once it finishes (state == "result"); skip "start" events.
        if (state == "start")
            return string.Empty;

        var hasError = props.TryGetProperty("error", out var errEl) && errEl.ValueKind == JsonValueKind.True;

        // Format as: [tool: bash] command here...
        var inputSummary = FormatToolInput(name, props);
        var errorSuffix = hasError ? " [error]" : string.Empty;
        return $"[tool: {name}]{inputSummary}{errorSuffix}";
    }

    private static string ParseSessionEvent(JsonElement root)
    {
        // Session info (cost, tokens) is the "done" signal from opencode.
        // Format a stats line with the special prefix so the frontend can render a card.
        JsonElement props = root;
        if (root.TryGetProperty("properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
            props = propsEl;

        // Tokens
        long? inputTokens = null;
        long? outputTokens = null;
        long? cacheReadTokens = null;
        long? cacheWriteTokens = null;

        if (props.TryGetProperty("tokens", out var tokensEl) && tokensEl.ValueKind == JsonValueKind.Object)
        {
            inputTokens = TryGetLong(tokensEl, "input");
            outputTokens = TryGetLong(tokensEl, "output");
            if (tokensEl.TryGetProperty("cache", out var cacheEl) && cacheEl.ValueKind == JsonValueKind.Object)
            {
                cacheReadTokens = TryGetLong(cacheEl, "read");
                cacheWriteTokens = TryGetLong(cacheEl, "write");
            }
        }

        // Cost
        double? cost = null;
        if (props.TryGetProperty("cost", out var costEl) && costEl.ValueKind == JsonValueKind.Number)
            cost = costEl.GetDouble();

        // Model
        var model = GetString(props, "model");

        // Emit nothing when there are no meaningful stats (e.g. session-start event).
        if (inputTokens is null && outputTokens is null && cost is null)
            return string.Empty;

        var stats = new
        {
            inputTokens,
            outputTokens,
            cacheReadTokens,
            cacheWriteTokens,
            cost,
            model,
        };

        return StatsPrefix + JsonSerializer.Serialize(stats);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static string? GetString(JsonElement el, string property) =>
        el.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static string? TryGetNestedString(JsonElement el, string firstKey, string secondKey)
    {
        if (!el.TryGetProperty(firstKey, out var inner) || inner.ValueKind != JsonValueKind.Object)
            return null;
        return GetString(inner, secondKey);
    }

    private static long? TryGetLong(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v))
            return v;
        return null;
    }

    private static string FormatToolInput(string toolName, JsonElement props)
    {
        if (!props.TryGetProperty("input", out var inputEl) || inputEl.ValueKind != JsonValueKind.Object)
            return string.Empty;

        // Format the most meaningful field for each common tool name.
        var summary = toolName switch
        {
            "bash" or "exec" => GetString(inputEl, "command") ?? GetString(inputEl, "cmd"),
            "read" => GetString(inputEl, "filePath") ?? GetString(inputEl, "path"),
            "write" or "edit" or "patch" => GetString(inputEl, "filePath") ?? GetString(inputEl, "path"),
            "search" or "grep" => GetString(inputEl, "pattern") ?? GetString(inputEl, "query"),
            "ls" or "list" => GetString(inputEl, "path") ?? GetString(inputEl, "dir"),
            _ => null,
        };

        return summary is not null ? $" {summary}" : string.Empty;
    }
}
