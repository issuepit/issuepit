using System.Text;
using System.Text.Json;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using IssuePit.Core.Runners;
using Renci.SshNet;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Connects to a remote host via SSH and launches the agent in a Docker container there.
/// 
/// Expected <see cref="RuntimeConfiguration.Configuration"/> JSON:
/// <code>
/// {
///   "Host": "192.168.1.1",
///   "Port": 22,
///   "Username": "ubuntu",
///   "PrivateKey": "-----BEGIN OPENSSH PRIVATE KEY-----\n..."
/// }
/// </code>
/// </summary>
public class SshDockerAgentRuntime(ILogger<SshDockerAgentRuntime> logger) : IAgentRuntime
{
    public async Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        GitRepository? gitRepository,
        Func<string, LogStream, Task> onLogLine,
        CancellationToken cancellationToken)
    {
        if (runtimeConfig is null)
            throw new InvalidOperationException("SshDockerAgentRuntime requires a RuntimeConfiguration.");

        var config = JsonSerializer.Deserialize<SshConfig>(runtimeConfig.Configuration,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Could not deserialize SSH RuntimeConfiguration.");

        if (string.IsNullOrWhiteSpace(config.Host))
            throw new InvalidOperationException("SSH RuntimeConfiguration missing 'Host'.");
        if (string.IsNullOrWhiteSpace(config.Username))
            throw new InvalidOperationException("SSH RuntimeConfiguration missing 'Username'.");

        return await RunDockerOverSshAsync(session, agent, issue, credentials, gitRepository, config, cancellationToken);
    }

    private async Task<string> RunDockerOverSshAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        GitRepository? gitRepository,
        SshConfig config,
        CancellationToken cancellationToken)
    {
        using var client = BuildSshClient(config);

        logger.LogInformation("Connecting to SSH host {Host}:{Port} for session {SessionId}",
            config.Host, config.Port, session.Id);

        await client.ConnectAsync(cancellationToken);

        try
        {
            var dockerCmd = BuildDockerRunCommand(session, agent, issue, credentials, gitRepository);
            logger.LogDebug("Running on {Host}: {Cmd}", config.Host, dockerCmd);

            using var cmd = client.CreateCommand(dockerCmd);
            await cmd.ExecuteAsync(cancellationToken);

            if (cmd.ExitStatus != 0)
                throw new InvalidOperationException(
                    $"docker run exited with code {cmd.ExitStatus}: {cmd.Error}");

            // `docker run -d` prints the full container ID on stdout
            var containerId = cmd.Result.Trim();
            logger.LogInformation("Started remote container {ContainerId} on {Host} for session {SessionId}",
                containerId, config.Host, session.Id);
            return containerId;
        }
        finally
        {
            client.Disconnect();
        }
    }

    private static SshClient BuildSshClient(SshConfig config)
    {
        var port = config.Port > 0 ? config.Port : 22;

        if (!string.IsNullOrWhiteSpace(config.PrivateKey))
        {
            // PrivateKeyFile reads the stream immediately in its constructor;
            // the MemoryStream is no longer needed after construction.
            var keyBytes = Encoding.UTF8.GetBytes(config.PrivateKey);
            using var keyStream = new MemoryStream(keyBytes);
            var keyFile = string.IsNullOrWhiteSpace(config.PrivateKeyPassphrase)
                ? new PrivateKeyFile(keyStream)
                : new PrivateKeyFile(keyStream, config.PrivateKeyPassphrase);
            return new SshClient(config.Host, port, config.Username, keyFile);
        }

        return new SshClient(config.Host, port, config.Username, config.Password ?? string.Empty);
    }

    private static string BuildDockerRunCommand(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        GitRepository? gitRepository)
    {
        var envArgs = new StringBuilder();

        // Build env vars using the shared helper and append as -e KEY=VALUE shell args.
        foreach (var entry in AgentEnvironmentBuilder.Build(session, agent, issue, credentials, gitRepository))
        {
            var eqIdx = entry.IndexOf('=');
            var key = entry[..eqIdx];
            var value = entry[(eqIdx + 1)..];
            envArgs.Append($" -e {EscapeShell(SanitizeValue(key))}={EscapeShell(SanitizeValue(value))}");
        }

        var labels =
            $"--label issuepit.session-id={session.Id} " +
            $"--label issuepit.issue-id={issue.Id} " +
            $"--label issuepit.agent-id={agent.Id}";

        // Append runner-specific CMD args (model, task) after the image name
        var runnerArgs = RunnerCommandBuilder.BuildArgs(agent, issue);
        var imageAndArgs = string.IsNullOrEmpty(runnerArgs)
            ? EscapeShell(agent.DockerImage)
            : $"{EscapeShell(agent.DockerImage)} {runnerArgs}";

        // -d = detached; --rm = auto-remove on exit; --privileged = true DinD (in-container dockerd)
        return $"docker run -d --rm --privileged {labels}{envArgs} {imageAndArgs}";
    }

    /// <summary>Removes control characters (null bytes, newlines, etc.) that could break shell argument parsing.</summary>
    private static string SanitizeValue(string value) =>
        new string(value.Where(c => c >= 0x20 || c == '\t').ToArray());

    /// <summary>Wraps a value in single quotes and escapes embedded single quotes.</summary>
    private static string EscapeShell(string value) =>
        $"'{value.Replace("'", "'\\''")}'";

    private record SshConfig(
        string Host,
        int Port,
        string Username,
        string? PrivateKey,
        string? PrivateKeyPassphrase,
        string? Password);
}
