using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Runners;

namespace IssuePit.Tests.Unit;

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

    private static Issue MakeIssue(string title = "Fix the bug", string? body = null, int number = 0) =>
        new() { Id = Guid.NewGuid(), Title = title, Body = body, Number = number };

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
        var issue = MakeIssue("Fix the bug", number: 7);
        var args = RunnerCommandBuilder.BuildArgs(agent, issue);
        Assert.StartsWith("run", args);
        Assert.Contains("Fix the bug", args);
        Assert.Contains("7", args);
    }

    [Fact]
    public void BuildArgs_OpenCode_ContainsFormatJsonFlag()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue();
        var args = RunnerCommandBuilder.BuildArgs(agent, issue);
        Assert.Contains("--format json", args);
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
    public void BuildArgsList_OpenCode_ContainsFormatJsonFlag()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue();
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue);
        Assert.Contains("--format", args);
        var formatIdx = args.ToList().IndexOf("--format");
        Assert.Equal("json", args[formatIdx + 1]);
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
        var issue = MakeIssue("Fix the bug", "Body text.", number: 5);
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.Contains("<number>5</number>", prompt);
        Assert.Contains("Fix the bug", prompt);
        Assert.Contains("Body text.", prompt);
        Assert.DoesNotContain("<comments>", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_AlwaysContainsCommitInstructions()
    {
        var issue = MakeIssue("Fix the bug", number: 1);
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.Contains("Commit your changes after each meaningful step", prompt);
        Assert.Contains("final commit", prompt);
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
        Assert.Contains("<comments>", prompt);
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
    public void BuildTaskPrompt_WithTriggeringComment_MarksThatCommentAsNew()
    {
        var issue = MakeIssue("Fix the bug");
        var commentId = Guid.NewGuid();
        issue.TriggeringCommentId = commentId;
        var comments = new List<IssueComment>
        {
            new() { Id = Guid.NewGuid(), IssueId = issue.Id, Body = "Old comment.", CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
            new() { Id = commentId, IssueId = issue.Id, Body = "New instruction: please add logging.", CreatedAt = DateTime.UtcNow },
        };
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue, comments);
        Assert.Contains("is_new=\"true\"", prompt);
        Assert.Contains("New instruction: please add logging.", prompt);
        Assert.Contains("<instruction>", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithSubIssues_IncludesSubIssuesSection()
    {
        var issue = MakeIssue("Parent issue");
        var sub = MakeIssue("Sub-issue A", number: 10);
        issue.PromptSubIssues = [sub];
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.Contains("<sub_issues>", prompt);
        Assert.Contains("Sub-issue A", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithTasks_IncludesTasksSection()
    {
        var issue = MakeIssue("Issue with tasks");
        issue.PromptTasks = [new IssueTask { Id = Guid.NewGuid(), IssueId = issue.Id, Title = "Write unit tests", Status = IssueStatus.Todo }];
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.Contains("<tasks>", prompt);
        Assert.Contains("Write unit tests", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithAttachments_IncludesAttachmentsSection()
    {
        var issue = MakeIssue("Issue with attachment");
        issue.PromptAttachments = [new IssueAttachment { Id = Guid.NewGuid(), IssueId = issue.Id, FileName = "diagram.png", FileUrl = "https://example.com/diagram.png", ContentType = "image/png" }];
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.Contains("<attachments>", prompt);
        Assert.Contains("diagram.png", prompt);
        Assert.Contains("https://example.com/diagram.png", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithSimilarIssues_IncludesSimilarIssuesSection()
    {
        var issue = MakeIssue("Issue with similar");
        var similarIssue = MakeIssue("Similar feature request", number: 42);
        issue.PromptSimilarIssues =
        [
            new SimilarIssuePair { Id = Guid.NewGuid(), IssueId = issue.Id, SimilarIssueId = similarIssue.Id, SimilarIssue = similarIssue, Score = 0.85f, Reason = "Both relate to authentication" },
        ];
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.Contains("<similar_issues>", prompt);
        Assert.Contains("Similar feature request", prompt);
        Assert.Contains("number=\"42\"", prompt);
        Assert.Contains("score=\"0.85\"", prompt);
        Assert.Contains("Both relate to authentication", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithSimilarIssues_NoReason_OmitsReasonAttribute()
    {
        var issue = MakeIssue("Issue with similar no reason");
        var similarIssue = MakeIssue("Another issue", number: 7);
        issue.PromptSimilarIssues =
        [
            new SimilarIssuePair { Id = Guid.NewGuid(), IssueId = issue.Id, SimilarIssueId = similarIssue.Id, SimilarIssue = similarIssue, Score = 0.60f, Reason = null },
        ];
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.Contains("<similar_issues>", prompt);
        Assert.DoesNotContain("reason=", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithCiCdRuns_IncludesCiCdRunsSection()
    {
        var issue = MakeIssue("Issue with runs");
        issue.PromptCiCdRuns =
        [
            new CiCdRun { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), CommitSha = "abc123def456", Branch = "main", Workflow = "ci.yml", Status = IssuePit.Core.Enums.CiCdRunStatus.Failed, StartedAt = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc) },
        ];
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.Contains("limited_last_x=\"1\"", prompt);
        Assert.Contains("status=\"Failed\"", prompt);
        Assert.Contains("branch=\"main\"", prompt);
        Assert.Contains("workflow=\"ci.yml\"", prompt);
        Assert.Contains("commit=\"abc123de\"", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithoutSimilarIssues_DoesNotIncludeSimilarSection()
    {
        var issue = MakeIssue("Issue without similar");
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.DoesNotContain("<similar_issues>", prompt);
    }

    [Fact]
    public void BuildTaskPrompt_WithoutCiCdRuns_DoesNotIncludeRunsSection()
    {
        var issue = MakeIssue("Issue without runs");
        var prompt = RunnerCommandBuilder.BuildTaskPrompt(issue);
        Assert.DoesNotContain("<cicd_runs", prompt);
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

    // --- Session continuation / fork ---

    [Fact]
    public void BuildArgsList_OpenCode_WithForkSessionId_IncludesForkFlags()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Fix a bug");
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue, forkSessionId: "ses_abc123");
        Assert.Contains("--session", args);
        Assert.Contains("ses_abc123", args);
        Assert.Contains("--fork", args);
        // fork flag should immediately follow --session + id
        var argsList = args.ToList();
        var sessionIdx = argsList.IndexOf("--session");
        Assert.Equal("ses_abc123", argsList[sessionIdx + 1]);
        Assert.Equal("--fork", argsList[sessionIdx + 2]);
    }

    [Fact]
    public void BuildArgsList_OpenCode_WithContinueSessionId_IncludesSessionFlagWithoutFork()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Continue work");
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue, continueSessionId: "ses_xyz789");
        Assert.Contains("--session", args);
        Assert.Contains("ses_xyz789", args);
        Assert.DoesNotContain("--fork", args);
    }

    [Fact]
    public void BuildArgsList_OpenCode_ForkTakesPrecedenceOverContinue()
    {
        // When both forkSessionId and continueSessionId are provided, fork takes precedence.
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Fix");
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue, forkSessionId: "ses_fork", continueSessionId: "ses_cont");
        Assert.Contains("ses_fork", args);
        Assert.Contains("--fork", args);
        Assert.DoesNotContain("ses_cont", args);
    }

    // --- File attachment (--file) ---

    [Fact]
    public void BuildArgsList_OpenCode_WithFilePaths_IncludesFileFlags()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Fix with attachment");
        var paths = new List<string> { "/tmp/issuepit-attachments/diagram.png", "/tmp/issuepit-attachments/spec.pdf" };
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue, filePaths: paths);
        var argsList = args.ToList();
        Assert.Contains("--file", argsList);
        Assert.Contains("/tmp/issuepit-attachments/diagram.png", argsList);
        Assert.Contains("/tmp/issuepit-attachments/spec.pdf", argsList);
        // Each --file flag should be followed immediately by its path.
        var firstFileIdx = argsList.IndexOf("--file");
        Assert.Equal("/tmp/issuepit-attachments/diagram.png", argsList[firstFileIdx + 1]);
        var secondFileIdx = argsList.LastIndexOf("--file");
        Assert.Equal("/tmp/issuepit-attachments/spec.pdf", argsList[secondFileIdx + 1]);
    }

    [Fact]
    public void BuildArgsList_OpenCode_WithFilePaths_FilesFlagBeforeTask()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Fix with attachment");
        var paths = new List<string> { "/tmp/issuepit-attachments/notes.txt" };
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue, filePaths: paths);
        var argsList = args.ToList();
        var fileIdx = argsList.IndexOf("--file");
        // The task string is always the last element.
        Assert.True(fileIdx < argsList.Count - 1, "--file should appear before the task");
    }

    [Fact]
    public void BuildArgsList_OpenCode_NoFilePaths_NoFileFlag()
    {
        var agent = MakeAgent(RunnerType.OpenCode);
        var issue = MakeIssue("Fix without attachment");
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue);
        Assert.DoesNotContain("--file", args);
    }

    [Fact]
    public void BuildArgsList_Codex_WithFilePaths_IgnoresFilePaths()
    {
        // --file is opencode-specific; Codex should not include it even when paths are provided.
        var agent = MakeAgent(RunnerType.Codex);
        var issue = MakeIssue("Fix");
        var paths = new List<string> { "/tmp/issuepit-attachments/diagram.png" };
        var args = RunnerCommandBuilder.BuildArgsList(agent, issue, filePaths: paths);
        Assert.DoesNotContain("--file", args);
    }
}
