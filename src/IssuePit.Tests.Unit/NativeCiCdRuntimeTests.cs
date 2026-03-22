using IssuePit.CiCdClient.Runtimes;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class NativeCiCdRuntimeTests
{
    private static TriggerPayload Trigger(
        string? eventName = null,
        string? workflow = null,
        string? actEnv = null,
        string? actVars = null,
        string? actSecrets = null) =>
        new(
            ProjectId: Guid.NewGuid(),
            CommitSha: null,
            Branch: null,
            Workflow: workflow,
            AgentSessionId: null,
            WorkspacePath: null,
            EventName: eventName,
            ActEnv: actEnv,
            ActVars: actVars,
            ActSecrets: actSecrets);

    [Fact]
    public void BuildActArgumentsList_DefaultEventName_IsPush()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.Equal("push", args[0]);
    }

    [Fact]
    public void BuildActArgumentsList_CustomEventName_IsUsed()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(eventName: "pull_request"));
        Assert.Equal("pull_request", args[0]);
    }

    [Fact]
    public void BuildActArgumentsList_WithWorkflow_EmitsWFlag()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(workflow: ".github/workflows/ci.yml"));
        Assert.Contains("-W", args);
        var idx = args.ToList().IndexOf("-W");
        Assert.Equal(".github/workflows/ci.yml", args[idx + 1]);
    }

    [Fact]
    public void BuildActArgumentsList_BareWorkflowFilename_NormalizedToGitHubWorkflowsPath()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(workflow: "ci.yml"));
        Assert.Contains("-W", args);
        var idx = args.ToList().IndexOf("-W");
        Assert.Equal(Path.Combine(".github", "workflows", "ci.yml"), args[idx + 1]);
    }

    [Fact]
    public void BuildActArgumentsList_NoActEnvOrSecrets_NoExtraArgs()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.DoesNotContain("--env", args);
        Assert.DoesNotContain("--var", args);
        Assert.DoesNotContain("--secret", args);
    }

    [Fact]
    public void BuildActArgumentsList_WithActEnv_EmitsEnvFlags()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(actEnv: "FOO=bar\nBAZ=qux"));
        var list = args.ToList();
        Assert.Contains("--env", list);
        var idx = list.IndexOf("--env");
        Assert.Equal("FOO=bar", list[idx + 1]);
        Assert.Equal("--env", list[idx + 2]);
        Assert.Equal("BAZ=qux", list[idx + 3]);
    }

    [Fact]
    public void BuildActArgumentsList_WithActVars_EmitsVarFlags()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(actVars: "ISSUEPIT_RUN=true\nISSUEPIT_PROJECT_ID=abc"));
        var list = args.ToList();
        Assert.Contains("--var", list);
        var idx = list.IndexOf("--var");
        Assert.Equal("ISSUEPIT_RUN=true", list[idx + 1]);
        Assert.Equal("--var", list[idx + 2]);
        Assert.Equal("ISSUEPIT_PROJECT_ID=abc", list[idx + 3]);
    }

    [Fact]
    public void BuildActArgumentsList_NoActVars_NoVarFlags()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.DoesNotContain("--var", args);
    }

    [Fact]
    public void BuildActArgumentsList_WithActSecrets_EmitsSecretFlags()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(actSecrets: "GITHUB_TOKEN=ghp_xxx\nNPM_TOKEN=npm_yyy"));
        var list = args.ToList();
        Assert.Contains("--secret", list);
        var idx = list.IndexOf("--secret");
        Assert.Equal("GITHUB_TOKEN=ghp_xxx", list[idx + 1]);
        Assert.Equal("--secret", list[idx + 2]);
        Assert.Equal("NPM_TOKEN=npm_yyy", list[idx + 3]);
    }

    [Fact]
    public void BuildActArgumentsList_ActEnvWithBlankLines_SkipsBlanks()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(actEnv: "FOO=bar\n\n  \nBAZ=qux"));
        var list = args.ToList();
        var envArgs = new List<string>();
        for (var i = 0; i < list.Count - 1; i++)
        {
            if (list[i] == "--env")
                envArgs.Add(list[i + 1]);
        }
        Assert.Equal(["FOO=bar", "BAZ=qux"], envArgs);
    }

    [Fact]
    public void BuildActArgumentsList_ActEnvWithoutEqualsSign_IsSkipped()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(actEnv: "FOO=bar\nINVALIDLINE\nBAZ=qux"));
        var list = args.ToList();
        var envArgs = new List<string>();
        for (var i = 0; i < list.Count - 1; i++)
        {
            if (list[i] == "--env")
                envArgs.Add(list[i + 1]);
        }
        Assert.Equal(["FOO=bar", "BAZ=qux"], envArgs);
    }

    [Fact]
    public void BuildActArgumentsList_ActEnvMissingKey_IsSkipped()
    {
        // Lines starting with '=' (no key) should be skipped
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(actEnv: "FOO=bar\n=nokey\nBAZ=qux"));
        var list = args.ToList();
        var envArgs = new List<string>();
        for (var i = 0; i < list.Count - 1; i++)
        {
            if (list[i] == "--env")
                envArgs.Add(list[i + 1]);
        }
        Assert.Equal(["FOO=bar", "BAZ=qux"], envArgs);
    }

    [Fact]
    public void BuildActArgumentsList_AlwaysIncludesJsonFlag()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.Contains("--json", args);
    }

    [Fact]
    public void BuildActArgumentsList_JsonFlagBeforeWorkflowFlag()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(workflow: "ci.yml"));
        var list = args.ToList();
        var jsonIdx = list.IndexOf("--json");
        var wIdx = list.IndexOf("-W");
        Assert.True(jsonIdx >= 0);
        Assert.True(wIdx >= 0);
        Assert.True(jsonIdx < wIdx);
    }

    [Fact]
    public void ParseKeyValuePairs_NullInput_ReturnsEmpty()
    {
        Assert.Empty(NativeCiCdRuntime.ParseKeyValuePairs(null));
    }

    [Fact]
    public void ParseKeyValuePairs_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(NativeCiCdRuntime.ParseKeyValuePairs(""));
    }

    [Fact]
    public void ParseKeyValuePairs_ValidPairs_ParsedCorrectly()
    {
        var result = NativeCiCdRuntime.ParseKeyValuePairs("KEY1=val1\nKEY2=val2").ToList();
        Assert.Equal(["KEY1=val1", "KEY2=val2"], result);
    }

    [Fact]
    public void ParseKeyValuePairs_ValueWithEquals_FullValuePreserved()
    {
        var result = NativeCiCdRuntime.ParseKeyValuePairs("URL=https://example.com?a=b").ToList();
        Assert.Single(result);
        Assert.Equal("URL=https://example.com?a=b", result[0]);
    }

    [Fact]
    public void BuildActArgumentsList_WithInputs_EmitsInputFlags()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null,
            Branch: null,
            Workflow: null,
            AgentSessionId: null,
            WorkspacePath: null,
            EventName: "workflow_dispatch",
            Inputs: new Dictionary<string, string>
            {
                ["environment"] = "staging",
                ["version"] = "1.0.0",
            });

        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger).ToList();

        Assert.Contains("--input", args);
        var envIdx = args.IndexOf("--input");
        Assert.Equal("environment=staging", args[envIdx + 1]);
        Assert.Equal("--input", args[envIdx + 2]);
        Assert.Equal("version=1.0.0", args[envIdx + 3]);
    }

    [Fact]
    public void BuildActArgumentsList_NullInputs_NoInputFlags()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger(eventName: "workflow_dispatch"));
        Assert.DoesNotContain("--input", args);
    }

    [Fact]
    public void BuildActArgumentsList_ConcurrentJobs_NeverEmitsFlag()
    {
        // --concurrent-jobs is not a valid flag in act v0.2.x; it must never be emitted.
        var defaultArgs = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.DoesNotContain("--concurrent-jobs", defaultArgs);

        var customArgs = NativeCiCdRuntime.BuildActArgumentsList(new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            ConcurrentJobs: 2));
        Assert.DoesNotContain("--concurrent-jobs", customArgs);
    }

    [Fact]
    public void BuildActArgumentsList_WithActionCachePath_EmitsFlag()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            ActionCachePath: "/var/lib/act-cache");
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger).ToList();
        var idx = args.IndexOf("--action-cache-path");
        Assert.True(idx >= 0, "--action-cache-path flag should be present");
        Assert.Equal("/var/lib/act-cache", args[idx + 1]);
    }

    [Fact]
    public void BuildActArgumentsList_NoActionCachePath_NoFlag()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.DoesNotContain("--action-cache-path", args);
    }

    [Fact]
    public void BuildActArgumentsList_UseNewActionCache_EmitsFlag()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            UseNewActionCache: true);
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger);
        Assert.Contains("--use-new-action-cache", args);
    }

    [Fact]
    public void BuildActArgumentsList_UseNewActionCacheFalse_NoFlag()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            UseNewActionCache: false);
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger);
        Assert.DoesNotContain("--use-new-action-cache", args);
    }

    [Fact]
    public void BuildActArgumentsList_UseNewActionCacheNull_NoFlag()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.DoesNotContain("--use-new-action-cache", args);
    }

    [Fact]
    public void BuildActArgumentsList_ActionOfflineMode_EmitsFlag()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            ActionOfflineMode: true);
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger);
        Assert.Contains("--action-offline-mode", args);
    }

    [Fact]
    public void BuildActArgumentsList_ActionOfflineModeFalse_NoFlag()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            ActionOfflineMode: false);
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger);
        Assert.DoesNotContain("--action-offline-mode", args);
    }

    [Fact]
    public void BuildActArgumentsList_ActionOfflineModeNull_NoFlag()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.DoesNotContain("--action-offline-mode", args);
    }

    [Fact]
    public void BuildActArgumentsList_WithLocalRepositories_EmitsFlags()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            LocalRepositories: "myorg/private@v1=/local/path\nmyorg/other@main=/other/path");
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger).ToList();

        Assert.Contains("--local-repository", args);
        var firstIdx = args.IndexOf("--local-repository");
        Assert.Equal("myorg/private@v1=/local/path", args[firstIdx + 1]);
        Assert.Equal("--local-repository", args[firstIdx + 2]);
        Assert.Equal("myorg/other@main=/other/path", args[firstIdx + 3]);
    }

    [Fact]
    public void BuildActArgumentsList_NoLocalRepositories_NoFlag()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.DoesNotContain("--local-repository", args);
    }

    [Fact]
    public void BuildActArgumentsList_WithSkipSteps_EmitsSkipStepFlags()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            SkipSteps: "deploy\nbuild:upload-artifacts");
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger).ToList();

        Assert.Contains("--skip-step", args);
        var firstIdx = args.IndexOf("--skip-step");
        Assert.Equal("deploy", args[firstIdx + 1]);
        Assert.Equal("--skip-step", args[firstIdx + 2]);
        Assert.Equal("build:upload-artifacts", args[firstIdx + 3]);
    }

    [Fact]
    public void BuildActArgumentsList_NoSkipSteps_NoFlag()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.DoesNotContain("--skip-step", args);
    }

    [Fact]
    public void BuildActArgumentsList_SkipSteps_IgnoresBlankLines()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            SkipSteps: "deploy\n\n  \nbuild:upload-artifacts");
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger).ToList();

        var skipStepArgs = args
            .Select((a, i) => (a, i))
            .Where(t => t.a == "--skip-step")
            .Select(t => args[t.i + 1])
            .ToList();
        Assert.Equal(["deploy", "build:upload-artifacts"], skipStepArgs);
    }

    [Fact]
    public void BuildActArgumentsList_AllCacheFlags_CombinedCorrectly()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            ActionCachePath: "/cache/actions",
            UseNewActionCache: true,
            ActionOfflineMode: true,
            LocalRepositories: "owner/repo@v1=/local");
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger).ToList();
        Assert.Contains("--action-cache-path", args);
        Assert.Contains("--use-new-action-cache", args);
        Assert.Contains("--action-offline-mode", args);
        Assert.Contains("--local-repository", args);
    }
}
