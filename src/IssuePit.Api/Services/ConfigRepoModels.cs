using System.ComponentModel.DataAnnotations;
using IssuePit.Core.Enums;

namespace IssuePit.Api.Services;

/// <summary>
/// Collects the outcome of a config-repo sync: files processed, warnings, and validation errors.
/// Returned by <see cref="ConfigRepoApplier.ApplyAsync"/> and surfaced via the sync API endpoint.
/// </summary>
public class ConfigSyncResult
{
    public int FilesProcessed { get; set; }
    public List<ConfigSyncIssue> Issues { get; } = [];

    public bool HasErrors => Issues.Any(i => i.Severity == ConfigSyncSeverity.Error);

    /// <summary>
    /// True when at least one strict-mode warning was escalated to an error.
    /// Only this flag triggers a 422 response; regular parse/validation errors do not.
    /// </summary>
    public bool HasStrictModeErrors { get; private set; }

    public void AddWarning(string file, string message) =>
        Issues.Add(new ConfigSyncIssue(ConfigSyncSeverity.Warning, file, message));

    public void AddError(string file, string message) =>
        Issues.Add(new ConfigSyncIssue(ConfigSyncSeverity.Error, file, message));

    /// <summary>Records an error that was promoted from a warning because strict mode is active.</summary>
    public void AddStrictModeError(string file, string message)
    {
        HasStrictModeErrors = true;
        AddError(file, message);
    }
}

public record ConfigSyncIssue(ConfigSyncSeverity Severity, string File, string Message);

public enum ConfigSyncSeverity { Warning, Error }

/// <summary>JSON5 model for an organization config override file in the config repository (orgs/*.json5).</summary>
public class OrgConfigModel
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Slug { get; set; }

    [Range(0, 1000, ErrorMessage = "maxConcurrentRunners must be between 0 and 1000.")]
    public int? MaxConcurrentRunners { get; set; }

    [Range(0, 100, ErrorMessage = "concurrentJobs must be between 0 and 100.")]
    public int? ConcurrentJobs { get; set; }

    [MaxLength(500)]
    public string? ActRunnerImage { get; set; }

    public string? ActEnv { get; set; }
    public string? ActSecrets { get; set; }

    [MaxLength(500)]
    public string? ActionCachePath { get; set; }

    public bool? UseNewActionCache { get; set; }
    public bool? ActionOfflineMode { get; set; }
    public string? LocalRepositories { get; set; }
    public string? SkipSteps { get; set; }

    public List<OrgMemberConfigModel>? Members { get; set; }
}

/// <summary>JSON5 model for an organization member entry in a config override file.</summary>
public class OrgMemberConfigModel
{
    /// <summary>Optional user ID. Takes priority over <see cref="Username"/> when both are set.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Username of the member. Ignored when <see cref="UserId"/> is set.</summary>
    [MaxLength(200)]
    public string? Username { get; set; }

    public OrgRole Role { get; set; } = OrgRole.Member;
}

/// <summary>JSON5 model for a project config override file in the config repository (projects/*.json5).</summary>
public class ProjectConfigModel
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Slug { get; set; }

    /// <summary>Slug of the owning organization. Required when creating a new project from config.</summary>
    [MaxLength(100)]
    public string? OrgSlug { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Single git origin shorthand. When set, configures (or creates) a <c>Working</c>-mode
    /// origin for this project. Use <see cref="GitRepos"/> for multi-origin setups.
    /// </summary>
    [MaxLength(500)]
    public string? GitUrl { get; set; }

    /// <summary>PAT or access token for authenticating with <see cref="GitUrl"/>.</summary>
    [MaxLength(500)]
    public string? GitToken { get; set; }

    /// <summary>Username used with <see cref="GitToken"/> for HTTP basic auth (defaults to "git").</summary>
    [MaxLength(200)]
    public string? GitUsername { get; set; }

    /// <summary>Default branch name for <see cref="GitUrl"/>. Defaults to "main".</summary>
    [MaxLength(200)]
    public string? DefaultBranch { get; set; }

    /// <summary>
    /// Full list of git origins for this project. When set, the applier upserts all origins
    /// in this list (matched by <c>remoteUrl</c>).
    /// Takes precedence over the single-origin <see cref="GitUrl"/> shorthand.
    /// </summary>
    public List<GitRepoConfigModel>? GitRepos { get; set; }

    public bool? MountRepositoryInDocker { get; set; }

    [Range(0, 1000, ErrorMessage = "maxConcurrentRunners must be between 0 and 1000.")]
    public int? MaxConcurrentRunners { get; set; }

    [Range(0, 100, ErrorMessage = "concurrentJobs must be between 0 and 100.")]
    public int? ConcurrentJobs { get; set; }

    [MaxLength(500)]
    public string? ActRunnerImage { get; set; }

    public string? ActEnv { get; set; }
    public string? ActSecrets { get; set; }

    [MaxLength(500)]
    public string? ActionCachePath { get; set; }

    public bool? UseNewActionCache { get; set; }
    public bool? ActionOfflineMode { get; set; }

    /// <summary>
    /// Newline-separated <c>owner/repo=localPath</c> mappings that redirect reusable workflow
    /// calls to local clones. Passed to <c>act</c> via <c>--local-repository</c> flags.
    /// </summary>
    public string? LocalRepositories { get; set; }

    /// <summary>
    /// Newline-separated list of step names or <c>job:step</c> pairs passed as
    /// <c>--skip-step</c> arguments to <c>act</c>.
    /// </summary>
    public string? SkipSteps { get; set; }

    public List<ProjectMemberConfigModel>? Members { get; set; }
}

/// <summary>JSON5 model for a single git origin within a project config file.</summary>
public class GitRepoConfigModel
{
    /// <summary>Remote git URL. Used as the unique key to match existing origins in the DB.</summary>
    [Required(ErrorMessage = "remoteUrl is required.")]
    [MaxLength(500)]
    public string RemoteUrl { get; set; } = string.Empty;

    /// <summary>PAT or access token for authenticating with <see cref="RemoteUrl"/>.</summary>
    [MaxLength(500)]
    public string? GitToken { get; set; }

    /// <summary>Username used with <see cref="GitToken"/> for HTTP basic auth (defaults to "git").</summary>
    [MaxLength(200)]
    public string? GitUsername { get; set; }

    /// <summary>Default branch name. Defaults to "main".</summary>
    [MaxLength(200)]
    public string? DefaultBranch { get; set; }

    /// <summary>How this origin is used: <c>Working</c> (agents push here), <c>Release</c>, or <c>ReadOnly</c>.</summary>
    public GitOriginMode Mode { get; set; } = GitOriginMode.Working;
}

/// <summary>JSON5 model for a project member entry in a config override file.</summary>
public class ProjectMemberConfigModel
{
    /// <summary>Optional user ID. Takes priority over <see cref="Username"/> when both are set.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Username of the member. Ignored when <see cref="UserId"/> is set.</summary>
    [MaxLength(200)]
    public string? Username { get; set; }

    public ProjectPermission Permissions { get; set; } = ProjectPermission.Read;
}

/// <summary>JSON5 model for an agent config file in the config repository (agents/*.json5).</summary>
public class AgentConfigModel
{
    /// <summary>Display name of the agent. Used as the key to match an existing agent by name within the tenant.</summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    /// <summary>
    /// Runner type for the agent (e.g. "opencode", "codex", "githubcopilotcli").
    /// Maps to <see cref="RunnerType"/> enum values (case-insensitive).
    /// </summary>
    [MaxLength(100)]
    public string? Runner { get; set; }

    /// <summary>AI model identifier passed to the CLI runner (e.g. "anthropic/claude-opus-4-5").</summary>
    [MaxLength(200)]
    public string? Model { get; set; }

    /// <summary>System prompt (instructions for the AI work agent).</summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// Docker image for the agent container.
    /// Null or omitted means the field will not be changed by the config file.
    /// </summary>
    [MaxLength(500)]
    public string? DockerImage { get; set; }

    /// <summary>MCP server names to link to this agent. Must already exist in the MCP servers table.</summary>
    public List<string>? McpServers { get; set; }

    /// <summary>When true, a structured summary is saved as a Note after each agent session for future runs.</summary>
    public bool? AutoSummarize { get; set; }
}
