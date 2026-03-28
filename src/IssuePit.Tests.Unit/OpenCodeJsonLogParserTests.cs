using IssuePit.Core.Runners;

namespace IssuePit.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="OpenCodeJsonLogParser"/> which converts
/// <c>opencode run --format json</c> event lines to human-readable display text.
/// </summary>
[Trait("Category", "Unit")]
public class OpenCodeJsonLogParserTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Non-JSON input
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseLine_PlainText_ReturnedAsIs()
    {
        const string line = "Hello from opencode";
        Assert.Equal(line, OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_EmptyString_ReturnedAsIs()
    {
        Assert.Equal(string.Empty, OpenCodeJsonLogParser.ParseLine(string.Empty));
    }

    [Fact]
    public void ParseLine_InvalidJson_ReturnedAsIs()
    {
        const string line = "{not valid json}";
        Assert.Equal(line, OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_NonObjectJson_ReturnedAsIs()
    {
        const string line = """["array","value"]""";
        Assert.Equal(line, OpenCodeJsonLogParser.ParseLine(line));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Text events
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseLine_TextEvent_FlatFormat_ReturnsText()
    {
        const string line = """{"type":"text","text":"I'll fix this bug."}""";
        Assert.Equal("I'll fix this bug.", OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_TextEvent_NestedPropertiesFormat_ReturnsText()
    {
        const string line = """{"type":"text","properties":{"sessionID":"ses_abc","text":"Hello world"}}""";
        Assert.Equal("Hello world", OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_TextEvent_EmptyText_ReturnsEmpty()
    {
        const string line = """{"type":"text","text":""}""";
        Assert.Equal(string.Empty, OpenCodeJsonLogParser.ParseLine(line));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Tool events
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseLine_ToolEvent_BashCommand_IncludesCommandInOutput()
    {
        const string line = """{"type":"tool","name":"bash","input":{"command":"ls -la"},"output":"file.txt","error":false}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.Contains("[tool: bash]", result);
        Assert.Contains("ls -la", result);
    }

    [Fact]
    public void ParseLine_ToolEvent_ReadFile_IncludesPath()
    {
        const string line = """{"type":"tool","name":"read","input":{"filePath":"/src/foo.cs"},"output":"..."}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.Contains("[tool: read]", result);
        Assert.Contains("/src/foo.cs", result);
    }

    [Fact]
    public void ParseLine_ToolEvent_WithError_IncludesErrorMarker()
    {
        const string line = """{"type":"tool","name":"bash","input":{"command":"build"},"output":"error: not found","error":true}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.Contains("[tool: bash]", result);
        Assert.Contains("[error]", result);
    }

    [Fact]
    public void ParseLine_ToolEvent_NestedPropertiesFormat_ParsesCorrectly()
    {
        const string line = """{"type":"tool","properties":{"name":"bash","input":{"command":"git status"},"output":"clean","error":false}}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.Contains("[tool: bash]", result);
        Assert.Contains("git status", result);
    }

    [Fact]
    public void ParseLine_ToolEvent_StartState_ReturnsEmpty()
    {
        // "start" state events should be silently dropped; only "result" events are shown.
        const string line = """{"type":"tool","name":"bash","input":{"command":"ls"},"state":"start"}""";
        Assert.Equal(string.Empty, OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_ToolEvent_ToolNestedInsideProperties_ParsesCorrectly()
    {
        const string line = """{"type":"tool","properties":{"tool":{"name":"bash","input":{"command":"npm test"},"output":"ok","error":false}}}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.Contains("[tool: bash]", result);
        Assert.Contains("npm test", result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Session / stats events
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseLine_SessionEvent_WithTokensAndCost_ReturnsStatsLine()
    {
        const string line = """{"type":"session","cost":0.0045,"tokens":{"input":1234,"output":567},"model":"anthropic/claude-sonnet-4-5"}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.StartsWith(OpenCodeJsonLogParser.StatsPrefix, result);
        Assert.Contains("1234", result);
        Assert.Contains("567", result);
        Assert.Contains("0.0045", result);
    }

    [Fact]
    public void ParseLine_SessionEvent_NestedPropertiesFormat_ReturnsStatsLine()
    {
        const string line = """{"type":"session","properties":{"id":"ses_abc","cost":0.01,"tokens":{"input":100,"output":50,"cache":{"read":10,"write":5}},"model":"openai/gpt-4o"}}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.StartsWith(OpenCodeJsonLogParser.StatsPrefix, result);
        Assert.Contains("100", result);
        Assert.Contains("50", result);
        Assert.Contains("0.01", result);
    }

    [Fact]
    public void ParseLine_SessionEvent_NoTokensNoCost_ReturnsEmpty()
    {
        // Session events without stats (e.g. session-start) should be silently dropped.
        const string line = """{"type":"session","id":"ses_abc","title":"My session"}""";
        Assert.Equal(string.Empty, OpenCodeJsonLogParser.ParseLine(line));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Unknown event types
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseLine_UnknownEventType_ReturnedAsIs()
    {
        const string line = """{"type":"unknown_future_event","data":"something"}""";
        Assert.Equal(line, OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_NoTypeField_ReturnedAsIs()
    {
        const string line = """{"message":"no type here"}""";
        Assert.Equal(line, OpenCodeJsonLogParser.ParseLine(line));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // New opencode format: step_start / tool_use / step_finish
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseLine_StepStart_ReturnsStepStartMarker()
    {
        const string line = """{"type":"step_start","timestamp":1774585963911,"sessionID":"ses_abc","part":{"id":"prt_1","type":"step-start","snapshot":"abc123"}}""";
        Assert.Equal(OpenCodeJsonLogParser.StepStartMarker, OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_StepFinish_WithTokens_ReturnsStepFinishStatsLine()
    {
        const string line = """{"type":"step_finish","timestamp":1774585965612,"sessionID":"ses_abc","part":{"id":"prt_2","type":"step-finish","reason":"tool-calls","cost":0,"tokens":{"total":11126,"input":61,"output":134,"reasoning":0,"cache":{"read":0,"write":10931}}}}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.StartsWith(OpenCodeJsonLogParser.StepFinishPrefix, result);
        Assert.Contains("61", result);
        Assert.Contains("134", result);
    }

    [Fact]
    public void ParseLine_StepFinish_NoTokens_ReturnsEmpty()
    {
        // A step_finish without token data should return empty string (silently dropped).
        const string line = """{"type":"step_finish","timestamp":1000,"sessionID":"ses_abc","part":{"id":"prt_3","type":"step-finish","reason":"done"}}""";
        Assert.Equal(string.Empty, OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_ToolUse_Completed_ReturnsFormattedLine()
    {
        const string line = """{"type":"tool_use","timestamp":1774585965531,"sessionID":"ses_abc","part":{"id":"prt_3","type":"tool","callID":"call_1","tool":"glob","state":{"status":"completed","input":{"pattern":"**/*Dockerfile*"},"output":"/workspace/Dockerfile","time":{"start":1774585965514,"end":1774585965530}}}}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.Contains("[tool: glob]", result);
        Assert.Contains("**/*Dockerfile*", result);
        Assert.Contains("16ms", result);
    }

    [Fact]
    public void ParseLine_ToolUse_CompletedReadFile_IncludesPath()
    {
        const string line = """{"type":"tool_use","timestamp":1774585968394,"sessionID":"ses_abc","part":{"id":"prt_4","type":"tool","callID":"call_2","tool":"read","state":{"status":"completed","input":{"filePath":"/workspace/docker/Dockerfile.api"},"output":"...","time":{"start":1774585968389,"end":1774585968394}}}}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.Contains("[tool: read]", result);
        Assert.Contains("/workspace/docker/Dockerfile.api", result);
        Assert.Contains("5ms", result);
    }

    [Fact]
    public void ParseLine_ToolUse_LongDuration_FormatsAsSeconds()
    {
        const string line = """{"type":"tool_use","timestamp":1000,"sessionID":"ses_abc","part":{"id":"prt_5","type":"tool","callID":"call_3","tool":"bash","state":{"status":"completed","input":{"command":"npm run build"},"output":"ok","time":{"start":1000,"end":3500}}}}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.Contains("[tool: bash]", result);
        Assert.Contains("npm run build", result);
        Assert.Contains("2.5s", result);
    }

    [Fact]
    public void ParseLine_ToolUse_NotCompleted_ReturnsEmpty()
    {
        const string line = """{"type":"tool_use","timestamp":1000,"sessionID":"ses_abc","part":{"id":"prt_6","type":"tool","callID":"call_4","tool":"glob","state":{"status":"running","input":{"pattern":"**/*.cs"}}}}""";
        Assert.Equal(string.Empty, OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_ToolUse_MissingPart_ReturnsEmpty()
    {
        const string line = """{"type":"tool_use","timestamp":1000,"sessionID":"ses_abc"}""";
        Assert.Equal(string.Empty, OpenCodeJsonLogParser.ParseLine(line));
    }

    [Fact]
    public void ParseLine_ToolUse_NoDuration_OmitsDurationSuffix()
    {
        const string line = """{"type":"tool_use","timestamp":1000,"sessionID":"ses_abc","part":{"id":"prt_7","type":"tool","callID":"call_5","tool":"glob","state":{"status":"completed","input":{"pattern":"*.json"}}}}""";
        var result = OpenCodeJsonLogParser.ParseLine(line);
        Assert.Contains("[tool: glob]", result);
        Assert.Contains("*.json", result);
        Assert.DoesNotContain("[", result.Replace("[tool: glob]", ""));
    }
}
