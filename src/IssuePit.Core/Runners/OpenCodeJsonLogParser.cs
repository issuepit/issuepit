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
///   <item><c>step_start</c> — step boundary marker (new opencode format); emits <see cref="StepStartMarker"/>
///     so the frontend can group subsequent tool calls into a collapsible step block.</item>
///   <item><c>tool_use</c> — tool invocation in new opencode format; formatted as a summary line with duration.</item>
///   <item><c>step_finish</c> — step boundary marker (new opencode format); emits a
///     <see cref="StepFinishPrefix"/> stats line with per-step token and cost data.</item>
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
    /// Prefix that identifies a formatted opencode session stats line stored in the database.
    /// The frontend detects this prefix and renders a stats card instead of plain text.
    /// </summary>
    public const string StatsPrefix = "[opencode:stats] ";

    /// <summary>
    /// Marker that identifies an opencode step-start line stored in the database.
    /// The frontend uses this to open a new collapsible step group in the log view.
    /// </summary>
    public const string StepStartMarker = "[opencode:step-start]";

    /// <summary>
    /// Prefix that identifies an opencode step-finish stats line stored in the database.
    /// The frontend detects this prefix and renders a compact per-step stats badge.
    /// </summary>
    public const string StepFinishPrefix = "[opencode:step-finish] ";

    /// <summary>
    /// Prefix that identifies a task-prompt log line stored in the database.
    /// The frontend detects this prefix and renders a collapsible prompt block (collapsed by default).
    /// The payload is a JSON object with a <c>text</c> property containing the full prompt text.
    /// </summary>
    public const string PromptPrefix = "[opencode:prompt] ";

    /// <summary>
    /// Prefix that identifies the beginning of a command-execution block.
    /// The frontend groups all subsequent log lines until <see cref="CmdEndMarker"/>
    /// into a single collapsible cmd block (collapsed by default).
    /// </summary>
    public const string CmdBeginPrefix = "[opencode:cmd-begin] ";

    /// <summary>
    /// Marker that closes a command-execution block opened by <see cref="CmdBeginPrefix"/>.
    /// </summary>
    public const string CmdEndMarker = "[opencode:cmd-end]";

    /// <summary>
    /// Section prefix prepended to log lines emitted during CI/CD fix runs (and other exec-based
    /// fix sections such as <c>UncommittedChangesFix</c> and <c>MessageRun</c>).
    /// <see cref="ParseLine"/> strips this prefix before parsing so that fix-run log lines are
    /// handled identically to primary-run log lines.
    /// </summary>
    private const string FixSectionPrefix = "[fix] ";

    /// <summary>
    /// Tries to parse <paramref name="rawLine"/> as an opencode JSON event and returns a
    /// human-readable display string. Returns the original line unchanged when it is not
    /// valid JSON or has an unrecognised event type.
    ///
    /// Lines that carry the <see cref="FixSectionPrefix"/> are handled transparently: the prefix
    /// is stripped before parsing and re-prepended to the parsed result, so callers do not need
    /// to strip it themselves.
    /// </summary>
    public static string ParseLine(string rawLine)
    {
        // Strip the known section prefix (added by CI/CD fix runs) so the JSON payload
        // can be parsed independently of the enclosing section context.
        var prefix = rawLine.StartsWith(FixSectionPrefix, StringComparison.Ordinal)
            ? FixSectionPrefix
            : string.Empty;
        var json = prefix.Length > 0 ? rawLine[prefix.Length..] : rawLine;

        if (json.Length == 0 || json[0] != '{')
            return rawLine;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var type = GetString(root, "type");

            var parsed = type switch
            {
                "text" => ParseTextEvent(root),
                "tool" => ParseToolEvent(root),
                "session" => ParseSessionEvent(root),
                // New opencode format (opencode ≥ 0.3): step/tool events wrapped in a "part" envelope.
                "step_start" => StepStartMarker,
                "tool_use" => ParseToolUseEvent(root),
                "step_finish" => ParseStepFinishEvent(root),
                // Unrecognized type — return the raw JSON payload so it is re-prefixed below.
                _ => json,
            };

            if (parsed.Length == 0) return string.Empty;

            // Re-prepend the section prefix when present. For unrecognised types this reconstructs
            // the original raw line (prefix + json). For recognised types it scopes the parsed
            // result to the correct section (e.g. "[fix] [opencode:step-start]").
            return prefix.Length > 0 ? prefix + parsed : parsed;
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
        // opencode uses either a nested "properties" object, a "part" envelope (new format),
        // or flat fields at the root level.
        var text = TryGetNestedString(root, "properties", "text")
            ?? TryGetNestedString(root, "part", "text")
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

    private static string ParseToolUseEvent(JsonElement root)
    {
        // New opencode format: tool call wrapped in a "part" envelope.
        if (!root.TryGetProperty("part", out var part) || part.ValueKind != JsonValueKind.Object)
            return string.Empty;

        if (!part.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.Object)
            return string.Empty;

        // Only emit once the tool call has completed.
        var status = GetString(state, "status");
        if (status != "completed")
            return string.Empty;

        var toolName = GetString(part, "tool") ?? "unknown";

        // Calculate duration from state.time (millisecond Unix timestamps).
        string? durationStr = null;
        if (state.TryGetProperty("time", out var timeEl) && timeEl.ValueKind == JsonValueKind.Object)
        {
            var start = TryGetLong(timeEl, "start");
            var end = TryGetLong(timeEl, "end");
            if (start.HasValue && end.HasValue)
            {
                var ms = end.Value - start.Value;
                durationStr = ms >= 1000 ? $"{ms / 1000.0:0.##}s" : $"{ms}ms";
            }
        }

        // state has the "input" sub-property expected by FormatToolInput.
        var inputSummary = FormatToolInput(toolName, state);
        var durationSuffix = durationStr != null ? $" [{durationStr}]" : string.Empty;
        return $"[tool: {toolName}]{inputSummary}{durationSuffix}";
    }

    private static string ParseStepFinishEvent(JsonElement root)
    {
        // New opencode format: step-finish wrapped in a "part" envelope with per-step token/cost data.
        if (!root.TryGetProperty("part", out var part) || part.ValueKind != JsonValueKind.Object)
            return string.Empty;

        long? inputTokens = null;
        long? outputTokens = null;
        long? cacheReadTokens = null;
        long? cacheWriteTokens = null;

        if (part.TryGetProperty("tokens", out var tokensEl) && tokensEl.ValueKind == JsonValueKind.Object)
        {
            inputTokens = TryGetLong(tokensEl, "input");
            outputTokens = TryGetLong(tokensEl, "output");
            if (tokensEl.TryGetProperty("cache", out var cacheEl) && cacheEl.ValueKind == JsonValueKind.Object)
            {
                cacheReadTokens = TryGetLong(cacheEl, "read");
                cacheWriteTokens = TryGetLong(cacheEl, "write");
            }
        }

        double? cost = null;
        if (part.TryGetProperty("cost", out var costEl) && costEl.ValueKind == JsonValueKind.Number)
            cost = costEl.GetDouble();

        // Emit nothing when there are no meaningful stats.
        if (inputTokens is null && outputTokens is null)
            return string.Empty;

        var stats = new { inputTokens, outputTokens, cacheReadTokens, cacheWriteTokens, cost };
        return StepFinishPrefix + JsonSerializer.Serialize(stats);
    }

    private static string ParseSessionEvent(JsonElement root)
    {
        // Session info (cost, tokens) is the "done" signal from opencode.
        // Format a stats line with the special prefix so the frontend can render a card.
        //
        // Three possible layouts are handled:
        //   1. Old flat format:        {"type":"session","cost":N,"tokens":{...},"model":"..."}
        //   2. Old properties format:  {"type":"session","properties":{"cost":N,"tokens":{...},...}}
        //   3. New part format (≥0.3): {"type":"session","part":{"cost":N,"tokens":{...},...}}
        JsonElement props = root;
        if (root.TryGetProperty("part", out var partEl) && partEl.ValueKind == JsonValueKind.Object)
            props = partEl;
        else if (root.TryGetProperty("properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
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
            "glob" => GetString(inputEl, "pattern"),
            "ls" or "list" => GetString(inputEl, "path") ?? GetString(inputEl, "dir"),
            _ => null,
        };

        return summary is not null ? $" {summary}" : string.Empty;
    }
}
