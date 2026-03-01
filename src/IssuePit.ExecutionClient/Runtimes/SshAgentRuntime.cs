using System.Text;
using System.Text.Json;
using IssuePit.Core.Entities;
using Renci.SshNet;

namespace IssuePit.ExecutionClient.Runtimes;

/// <summary>
/// Connects to a remote host via SSH and runs the agent executable natively there (no Docker).
/// 
/// Expected <see cref="RuntimeConfiguration.Configuration"/> JSON:
/// <code>
/// {
///   "Host": "192.168.1.1",
///   "Port": 22,
///   "Username": "ubuntu",
///   "PrivateKey": "-----BEGIN OPENSSH PRIVATE KEY-----\n...",
///   "Command": "/usr/local/bin/opencode"
/// }
/// </code>
/// </summary>
public class SshAgentRuntime(ILogger<SshAgentRuntime> logger) : IAgentRuntime
{
    public async Task<string> LaunchAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        RuntimeConfiguration? runtimeConfig,
        CancellationToken cancellationToken)
    {
        if (runtimeConfig is null)
            throw new InvalidOperationException("SshAgentRuntime requires a RuntimeConfiguration.");

        var config = JsonSerializer.Deserialize<SshNativeConfig>(runtimeConfig.Configuration,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Could not deserialize SSH RuntimeConfiguration.");

        if (string.IsNullOrWhiteSpace(config.Host))
            throw new InvalidOperationException("SSH RuntimeConfiguration missing 'Host'.");
        if (string.IsNullOrWhiteSpace(config.Username))
            throw new InvalidOperationException("SSH RuntimeConfiguration missing 'Username'.");
        if (string.IsNullOrWhiteSpace(config.Command))
            throw new InvalidOperationException("SSH RuntimeConfiguration missing 'Command'.");

        return await RunNativeOverSshAsync(session, agent, issue, credentials, config, cancellationToken);
    }

    private async Task<string> RunNativeOverSshAsync(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        SshNativeConfig config,
        CancellationToken cancellationToken)
    {
        using var client = BuildSshClient(config);

        logger.LogInformation("Connecting to SSH host {Host}:{Port} for session {SessionId}",
            config.Host, config.Port, session.Id);

        await client.ConnectAsync(cancellationToken);

        try
        {
            // Run the agent command with env vars exported inline and nohup so it survives the SSH session
            var remoteCmd = BuildNativeCommand(session, agent, issue, credentials, config.Command);
            logger.LogDebug("Running on {Host}: {Cmd}", config.Host, remoteCmd);

            using var cmd = client.CreateCommand(remoteCmd);
            await cmd.ExecuteAsync(cancellationToken);

            if (cmd.ExitStatus != 0)
                throw new InvalidOperationException(
                    $"Agent command exited with code {cmd.ExitStatus}: {cmd.Error}");

            // nohup outputs the PID; use it as the runtime identifier
            var pid = cmd.Result.Trim();
            if (!int.TryParse(pid, out _))
                throw new InvalidOperationException(
                    $"Expected a PID from agent start command but got: '{pid}'");

            logger.LogInformation("Started native agent (PID {Pid}) on {Host} for session {SessionId}",
                pid, config.Host, session.Id);
            return pid;
        }
        finally
        {
            client.Disconnect();
        }
    }

    private static SshClient BuildSshClient(SshNativeConfig config)
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

    private static string BuildNativeCommand(
        AgentSession session,
        Agent agent,
        Issue issue,
        IReadOnlyDictionary<string, string> credentials,
        string command)
    {
        var envPrefix = new StringBuilder();

        void AppendEnv(string key, string value) =>
            envPrefix.Append($"{EscapeShell(SanitizeValue(key))}={EscapeShell(SanitizeValue(value))} ");

        AppendEnv("ISSUEPIT_SESSION_ID", session.Id.ToString());
        AppendEnv("ISSUEPIT_ISSUE_ID", issue.Id.ToString());
        AppendEnv("ISSUEPIT_ISSUE_TITLE", issue.Title);
        AppendEnv("ISSUEPIT_ISSUE_BODY", issue.Body ?? string.Empty);
        AppendEnv("ISSUEPIT_AGENT_ID", agent.Id.ToString());

        if (issue.GitBranch is not null)
            AppendEnv("ISSUEPIT_GIT_BRANCH", issue.GitBranch);

        foreach (var (key, value) in credentials)
            AppendEnv(key, value);

        // nohup + & disowns the process so it keeps running after the SSH session closes;
        // echo $! returns the PID for tracking
        return $"nohup env {envPrefix}{EscapeShell(command)} </dev/null >/dev/null 2>&1 & echo $!";
    }

    /// <summary>Removes control characters (null bytes, newlines, etc.) that could break shell argument parsing.</summary>
    private static string SanitizeValue(string value) =>
        new string(value.Where(c => c >= 0x20 || c == '\t').ToArray());

    /// <summary>Wraps a value in single quotes and escapes embedded single quotes.</summary>
    private static string EscapeShell(string value) =>
        $"'{value.Replace("'", "'\\''")}'";

    private record SshNativeConfig(
        string Host,
        int Port,
        string Username,
        string Command,
        string? PrivateKey,
        string? PrivateKeyPassphrase,
        string? Password);
}
