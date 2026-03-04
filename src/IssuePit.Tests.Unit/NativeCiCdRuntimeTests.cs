using IssuePit.CiCdClient.Runtimes;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class NativeCiCdRuntimeTests
{
    private static TriggerPayload Trigger(
        string? eventName = null,
        string? workflow = null,
        string? actEnv = null,
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
    public void BuildActArgumentsList_NoActEnvOrSecrets_NoExtraArgs()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger());
        Assert.DoesNotContain("--env", args);
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
    public void BuildActArgumentsList_DefaultConcurrentJobs_EmitsFlagWithValue4()
    {
        var args = NativeCiCdRuntime.BuildActArgumentsList(Trigger()).ToList();
        var idx = args.IndexOf("--concurrent-jobs");
        Assert.True(idx >= 0, "--concurrent-jobs flag should be present by default");
        Assert.Equal(NativeCiCdRuntime.DefaultConcurrentJobs.ToString(), args[idx + 1]);
    }

    [Fact]
    public void BuildActArgumentsList_CustomConcurrentJobs_EmitsFlagWithCustomValue()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            ConcurrentJobs: 2);
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger).ToList();
        var idx = args.IndexOf("--concurrent-jobs");
        Assert.True(idx >= 0);
        Assert.Equal("2", args[idx + 1]);
    }

    [Fact]
    public void BuildActArgumentsList_ZeroConcurrentJobs_NoFlag()
    {
        var trigger = new TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null, Branch: null, Workflow: null,
            AgentSessionId: null, WorkspacePath: null, EventName: null,
            ConcurrentJobs: 0);
        var args = NativeCiCdRuntime.BuildActArgumentsList(trigger);
        Assert.DoesNotContain("--concurrent-jobs", args);
    }
}
