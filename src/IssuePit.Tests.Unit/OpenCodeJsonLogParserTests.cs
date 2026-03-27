using IssuePit.Core.Runners;

namespace IssuePit.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="OpenCodeJsonLogParser"/> which converts
/// <c>opencode run --format json</c> event lines to human-readable display text.
/// </summary>
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
}
