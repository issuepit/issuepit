using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Runners;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class RunnerCommandBuilderTests
{
    private static Agent MakeAgent(RunnerType? runnerType = null, string? model = null, string systemPrompt = "Be helpful") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "test-agent",
            SystemPrompt = systemPrompt,
            DockerImage = "ghcr.io/sst/opencode:latest",
            RunnerType = runnerType,
            Model = model,
        };

    private static Issue MakeIssue(string title = "Fix the bug", string? body = null) =>
        new() { Id = Guid.NewGuid(), Title = title, Body = body };

    [Fact]
    public void BuildArgs_NoRunnerType_ReturnsEmpty()
    {
        var agent = MakeAgent();
        var issue = MakeIssue();
        Assert.Equal(string.Empty, RunnerCommandBuilder.BuildArgs(agent, issue));
    }

    [Fact]
    public void BuildArgs_OpenCode_ContainsTask()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Fix the bug");
        var args = RunnerCommandBuilder.BuildArgs(agent, issue);
        Assert.StartsWith("run", args);
        Assert.Contains("Fix the bug", args);
    }

    [Fact]
    public void BuildArgs_OpenCode_WithModel_ContainsModelFlag()
    {
        var agent = MakeAgent(RunnerType.OpenCode, model: "anthropic/claude-opus-4-5");
        var issue = MakeIssue();
        var args = RunnerCommandBuilder.BuildArgs(agent, issue);
        Assert.Contains("--model", args);
        Assert.Contains("anthropic/claude-opus-4-5", args);
    }

    [Fact]
    public void BuildArgs_OpenCode_WithoutModel_NoModelFlag()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue();
        var args = RunnerCommandBuilder.BuildArgs(agent, issue);
        Assert.DoesNotContain("--model", args);
    }

    [Fact]
    public void BuildArgs_Codex_ContainsFullAutoFlag()
    {
        var agent = MakeAgent(RunnerType.Codex);
        var issue = MakeIssue();
        var args = RunnerCommandBuilder.BuildArgs(agent, issue);
        Assert.Contains("--full-auto", args);
    }

    [Fact]
    public void BuildArgs_Codex_ContainsTask()
    {
        var agent = MakeAgent(RunnerType.Codex);
        var issue = MakeIssue("Implement feature X");
        var args = RunnerCommandBuilder.BuildArgs(agent, issue);
        Assert.Contains("Implement feature X", args);
    }

    [Fact]
    public void BuildArgs_GitHubCopilotCli_ContainsSuggest()
    {
        var agent = MakeAgent(RunnerType.GitHubCopilotCli);
        var issue = MakeIssue("Write tests");
        var args = RunnerCommandBuilder.BuildArgs(agent, issue);
        Assert.StartsWith("suggest", args);
        Assert.Contains("Write tests", args);
    }

    // --- BuildArgsList (Docker CMD format — includes binary name as first element) ---

    [Fact]
    public void BuildArgsList_NoRunnerType_ReturnsEmpty()
    {
        var agent = MakeAgent();
        var issue = MakeIssue();
        Assert.Empty(RunnerCommandBuilder.BuildArgsList(agent, issue));
    }

    [Fact]
    public void BuildArgsList_OpenCode_StartsWithOpencode()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Fix the bug");
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue);
        Assert.Equal("opencode", args[0]);
        Assert.Equal("run", args[1]);
    }

    [Fact]
    public void BuildArgsList_OpenCode_ContainsTask()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Fix the bug");
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue);
        Assert.Contains(args, a => a.Contains("Fix the bug"));
    }

    [Fact]
    public void BuildArgsList_OpenCode_WithModel_ContainsModelFlag()
    {
        var agent = MakeAgent(RunnerType.OpenCode, model: "anthropic/claude-opus-4-5");
        var issue = MakeIssue();
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue);
        Assert.Equal("opencode", args[0]);
        Assert.Equal("run", args[1]);
        Assert.Contains("--model", args);
        Assert.Contains("anthropic/claude-opus-4-5", args);
    }

    [Fact]
    public void BuildArgsList_Codex_StartsWithCodex()
    {
        var agent = MakeAgent(RunnerType.Codex);
        var issue = MakeIssue();
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue);
        Assert.Equal("codex", args[0]);
    }

    [Fact]
    public void BuildArgsList_Codex_ContainsFullAutoFlag()
    {
        var agent = MakeAgent(RunnerType.Codex);
        var issue = MakeIssue();
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue);
        Assert.Contains("--full-auto", args);
    }

    [Fact]
    public void BuildArgsList_GitHubCopilotCli_StartsWithGh()
    {
        var agent = MakeAgent(RunnerType.GitHubCopilotCli);
        var issue = MakeIssue("Write tests");
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue);
        Assert.Equal("gh", args[0]);
        Assert.Contains("copilot", args);
        Assert.Contains("suggest", args);
        Assert.Contains(args, a => a.Contains("Write tests"));
    }

    [Fact]
    public void BuildArgs_IncludesIssueBodyInTask()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Fix the bug", "The bug causes a crash on startup.");
        var args = RunnerCommandBuilder.BuildArgs(agent, issue);
        Assert.Contains("The bug causes a crash on startup.", args);
    }

    [Fact]
    public void BuildTaskPrompt_NoComments_DoesNotIncludeCommentsSection()
    {
        var issue = MakeIssue("Fix the bug", "Body text.");
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.Contains("Fix the bug", prompt);
        Assert.Contains("Body text.", prompt);
        Assert.DoesNotContain("## Comments", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithComments_IncludesCommentsSection()
    {
        var issue = MakeIssue("Fix the bug");
        var comments = new List<IssueComment>
        {
            new() { Id = Guid.NewGuid(), IssueId = issue.Id, Body = "Please also fix the tests.", CreatedAt = DateTime.UtcNow },
        };
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue, comments);
        Assert.Contains("## Comments", prompt);
        Assert.Contains("Please also fix the tests.", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithComments_IncludesAuthorName()
    {
        var issue = MakeIssue("Fix the bug");
        var user = new User { Id = Guid.NewGuid(), Username = "alice", Email = "alice@example.com", TenantId = Guid.NewGuid() };
        var comments = new List<IssueComment>
        {
            new() { Id = Guid.NewGuid(), IssueId = issue.Id, Body = "Great idea!", User = user, CreatedAt = DateTime.UtcNow },
        };
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue, comments);
        Assert.Contains("alice", prompt);
        Assert.Contains("Great idea!", prompt);
    }

    [Fact]
    public void BuildRunnerEnv_NoRunnerType_ReturnsEmpty()
    {
        var agent = MakeAgent();
        Assert.Empty(RunnerCommandBuilder.BuildRunnerEnv(agent));
    }

    [Fact]
    public void BuildRunnerEnv_OpenCode_InjectsSystemPromptEnvVar()
    {
        var agent = MakeAgent(RunnerType.OpenCode, systemPrompt: "You are a senior engineer.");
        var env = RunnerCommandBuilder.BuildRunnerEnv(agent);
        Assert.True(env.ContainsKey("OPENCODE_SYSTEM_PROMPT"));
        Assert.Equal("You are a senior engineer.", env["OPENCODE_SYSTEM_PROMPT"]);
    }

    [Fact]
    public void BuildRunnerEnv_Codex_InjectsSystemPromptEnvVar()
    {
        var agent = MakeAgent(RunnerType.Codex, systemPrompt: "You are a helpful assistant.");
        var env = RunnerCommandBuilder.BuildRunnerEnv(agent);
        Assert.True(env.ContainsKey("CODEX_SYSTEM_PROMPT"));
        Assert.Equal("You are a helpful assistant.", env["CODEX_SYSTEM_PROMPT"]);
    }

    [Fact]
    public void BuildRunnerEnv_GitHubCopilotCli_ReturnsEmpty()
    {
        var agent = MakeAgent(RunnerType.GitHubCopilotCli);
        Assert.Empty(RunnerCommandBuilder.BuildRunnerEnv(agent));
    }
}
