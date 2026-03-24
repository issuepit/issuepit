using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Runners;
using System.Text.Json;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Builds the environment variable list passed to agent containers.
/// Shared between <see cref="DockerAgentRuntime"/> and <see cref="SshDockerAgentRuntime"/>
/// to keep the two runtimes in sync.
/// </summary>
internal static class AgentEnvironmentBuilder
{
    /// <summary>
    /// Returns the environment variables for an agent container as a list of
    /// <c>KEY=VALUE</c> strings suitable for Docker's <c>Env</c> parameter.
    /// </summary>
    /// <param name="gitRepository">
    ///   The Working-mode push-target remote. Used for <c>ISSUEPIT_GIT_DEFAULT_BRANCH</c>
    ///   (the base branch used to name agent feature branches) when no separate
    ///   <paramref name="cloneRepository"/> is provided.
    /// </param>
    /// <param name="cloneRepository">
    ///   The clone-source remote (typically the one with the highest commit count). When
    ///   provided its URL and credentials are injected as <c>ISSUEPIT_GIT_REMOTE_URL</c> /
    ///   <c>ISSUEPIT_GIT_AUTH_*</c> so the container clones from the correct origin.
    ///   When <c>null</c> <paramref name="gitRepository"/> is used as the clone source too.
    ///
    ///   <para>
    ///     In the exec flow (modern path) the C# code drives the clone directly with
    ///     <see cref="DockerRuntimeBase.CloneWorkspaceAsync"/> so these env vars are not used
    ///     for cloning. They ARE still used by legacy-flow containers whose <c>entrypoint.sh</c>
    ///     performs the clone itself.
    ///   </para>
    /// </param>
    public static List<string> Build(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        GitRepository? gitRepository,
        GitRepository? cloneRepository = null,
        string? issuePitMcpUrl = null)
    {
        // Use the dedicated clone-source remote when it is different from the push-target.
        // Falls back to gitRepository so callers that only pass one remote still work.
        // Use the dedicated clone-source remote when it is different from the push-target.
        // Falls back to gitRepository so callers that only pass one remote still work.
        var cloneRepo = cloneRepository ?? gitRepository;
        var env = new List<string>
        {
            $"ISSUEPIT_SESSION_ID={session.Id}",
            $"ISSUEPIT_ISSUE_ID={issue.Id}",
            $"ISSUEPIT_ISSUE_NUMBER={issue.Number}",
            $"ISSUEPIT_ISSUE_TITLE={issue.Title}",
            $"ISSUEPIT_ISSUE_BODY={issue.Body ?? string.Empty}",
            $"ISSUEPIT_AGENT_ID={agent.Id}",
            $"ISSUEPIT_PROJECT_ID={issue.ProjectId}",
            $"ISSUEPIT_SYSTEM_PROMPT={agent.SystemPrompt}",
        };

        if (issue.GitBranch is not null)
            env.Add($"ISSUEPIT_GIT_BRANCH={issue.GitBranch}");

        // Pass the push policy so the in-container git wrapper and opencode plugin can
        // allow or block agent-initiated git push commands accordingly.
        env.Add($"ISSUEPIT_PUSH_POLICY={(int)session.PushPolicy}");

        // Inform the entrypoint whether internet access is disabled (used for DNS logging display).
        env.Add($"ISSUEPIT_DISABLE_INTERNET={agent.DisableInternet.ToString().ToLowerInvariant()}");

        // Inject git clone-source info so the container can clone the repo on startup
        // (used by legacy-flow entrypoint.sh). The clone source may differ from the push target:
        // we always clone from the remote with the most commits (typically Release/upstream) so
        // the agent works from the freshest code, while pushes go to the Working remote.
        if (cloneRepo is not null)
        {
            env.Add($"ISSUEPIT_GIT_REMOTE_URL={cloneRepo.RemoteUrl}");
            env.Add($"ISSUEPIT_GIT_DEFAULT_BRANCH={cloneRepo.DefaultBranch}");
            if (!string.IsNullOrEmpty(cloneRepo.AuthUsername))
                env.Add($"ISSUEPIT_GIT_AUTH_USERNAME={cloneRepo.AuthUsername}");
            if (!string.IsNullOrEmpty(cloneRepo.AuthToken))
                env.Add($"ISSUEPIT_GIT_AUTH_TOKEN={cloneRepo.AuthToken}");
        }

        // Inject agent logins / API key credentials as environment variables.
        foreach (var (key, value) in credentials)
            env.Add($"{key}={value}");

        // Runner-specific env vars (e.g. OPENCODE_SYSTEM_PROMPT, CODEX_SYSTEM_PROMPT).
        foreach (var (key, value) in RunnerCommandBuilder.BuildRunnerEnv(agent))
            env.Add($"{key}={value}");

        // Inject the IssuePit MCP server URL so the entrypoint can write the opencode config.
        if (!string.IsNullOrWhiteSpace(issuePitMcpUrl))
            env.Add($"ISSUEPIT_MCP_URL={issuePitMcpUrl}");

        // Note: the ephemeral MCP token is passed through the credentials dictionary
        // (keyed as ISSUEPIT_MCP_TOKEN) and added via the foreach loop above.

        // Inject the agents list (current agent + children) as JSON so the entrypoint can configure
        // the CLI with nested agent modes. Only one level of nesting is supported.
        var agentsJson = BuildAgentsJson(agent);
        if (!string.IsNullOrEmpty(agentsJson))
            env.Add($"ISSUEPIT_OPENCODE_AGENTS_JSON={agentsJson}");

        // Inject linked MCP server configs (URL + secrets as headers) so the entrypoint can add
        // them to the opencode config alongside the IssuePit MCP server.
        var extraMcpJson = BuildExtraMcpJson(agent);
        if (!string.IsNullOrEmpty(extraMcpJson))
            env.Add($"ISSUEPIT_OPENCODE_EXTRA_MCP_JSON={extraMcpJson}");

        return env;
    }

    /// <summary>
    /// Serialises the current agent and its direct children (one level only) into a JSON array
    /// for consumption by the container entrypoint when writing the opencode config.
    /// Format: [{ "name": "...", "model": "...", "prompt": "...", "agentType": "primary"|"subagent"|null }, ...]
    /// </summary>
    internal static string BuildAgentsJson(Agent agent)
    {
        var agents = new List<object>
        {
            new { name = agent.Name, model = agent.Model ?? string.Empty, prompt = agent.SystemPrompt, agentType = AgentTypeToString(agent.AgentType) },
        };

        foreach (var child in agent.ChildAgents)
            agents.Add(new { name = child.Name, model = child.Model ?? string.Empty, prompt = child.SystemPrompt, agentType = AgentTypeToString(child.AgentType) });

        return JsonSerializer.Serialize(agents);
    }

    /// <summary>Converts an <see cref="OpenCodeAgentType"/> to the opencode config string value.</summary>
    private static string? AgentTypeToString(OpenCodeAgentType? agentType) => agentType switch
    {
        OpenCodeAgentType.Primary => "primary",
        OpenCodeAgentType.SubAgent => "subagent",
        OpenCodeAgentType.All => "all",
        _ => null,
    };

    /// <summary>
    /// Serialises MCP servers linked to the agent into a JSON array for the container entrypoint
    /// to merge into the opencode config's <c>mcp</c> section.
    /// Each entry contains the server name (slug), URL, type, and any secrets as HTTP headers.
    /// Returns an empty string when no linked MCP servers are configured.
    /// </summary>
    internal static string BuildExtraMcpJson(Agent agent)
    {
        var linked = agent.AgentMcpServers
            .Where(ams => ams.McpServer is not null)
            .Select(ams => ams.McpServer)
            .ToList();

        if (linked.Count == 0)
            return string.Empty;

        var entries = linked.Select(s =>
        {
            // Resolve secrets scoped to this agent or global, with agent-scoped taking precedence.
            // McpSecretScope.Agent (3) > McpSecretScope.Global (0), so OrderByDescending puts agent first.
            var headers = s.Secrets
                .Where(sec => sec.Scope == McpSecretScope.Global ||
                              (sec.Scope == McpSecretScope.Agent && sec.ScopeId == agent.Id))
                .GroupBy(sec => sec.Key)
                .Select(g => g.OrderBy(sec => sec.Scope == McpSecretScope.Agent ? 0 : 1).First())
                .ToDictionary(sec => sec.Key, sec => DecryptMcpSecret(sec.EncryptedValue));

            return new
            {
                name = s.Name.ToLowerInvariant().Replace(' ', '-'),
                type = ResolveMcpType(s.Configuration),
                url = s.Url,
                headers = headers.Count > 0 ? (object)headers : null,
            };
        }).ToList();

        return JsonSerializer.Serialize(entries);
    }

    /// <summary>
    /// Reads the "type" field from the MCP server's JSON configuration and normalises it to a
    /// value accepted by opencode. Legacy values "http" and "sse" are mapped to "remote".
    /// Defaults to "remote" when the field is absent or the configuration cannot be parsed.
    /// </summary>
    private static string ResolveMcpType(string configuration)
    {
        try
        {
            var doc = JsonDocument.Parse(configuration);
            if (doc.RootElement.TryGetProperty("type", out var typeEl))
            {
                var raw = typeEl.GetString() ?? "remote";
                return NormalizeMcpType(raw);
            }
        }
        catch (JsonException) { }
        return "remote";
    }

    /// <summary>
    /// Maps legacy MCP type values to opencode-valid ones.
    /// opencode only accepts "local" and "remote"; "http" and "sse" are not valid.
    /// </summary>
    private static string NormalizeMcpType(string type) => type switch
    {
        "http" or "sse" => "remote",
        "local" => "local",
        _ => "remote",
    };

    /// <summary>Strips the "plain:" placeholder prefix. Production will use proper decryption.</summary>
    private static string DecryptMcpSecret(string encryptedValue) =>
        encryptedValue.StartsWith("plain:") ? encryptedValue["plain:".Length..] : encryptedValue;
}
