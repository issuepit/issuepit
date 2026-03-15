using IssuePit.Core.Enums;

namespace IssuePit.Api.Services;

/// <summary>JSON model for an organization config override file in the config repository (orgs/*.json).</summary>
public class OrgConfigModel
{
    public string? Name { get; set; }
    public string? Slug { get; set; }
    public int? MaxConcurrentRunners { get; set; }
    public int? ConcurrentJobs { get; set; }
    public string? ActRunnerImage { get; set; }
    public string? ActEnv { get; set; }
    public string? ActSecrets { get; set; }
    public string? ActionCachePath { get; set; }
    public bool? UseNewActionCache { get; set; }
    public bool? ActionOfflineMode { get; set; }
    public string? LocalRepositories { get; set; }
    public List<OrgMemberConfigModel>? Members { get; set; }
}

/// <summary>JSON model for an organization member entry in a config override file.</summary>
public class OrgMemberConfigModel
{
    /// <summary>Optional user ID. Takes priority over <see cref="Username"/> when both are set.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Username of the member. Ignored when <see cref="UserId"/> is set.</summary>
    public string? Username { get; set; }

    public OrgRole Role { get; set; } = OrgRole.Member;
}

/// <summary>JSON model for a project config override file in the config repository (projects/*.json).</summary>
public class ProjectConfigModel
{
    public string? Name { get; set; }
    public string? Slug { get; set; }

    /// <summary>Slug of the owning organization. Required when creating a new project from config.</summary>
    public string? OrgSlug { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Single git origin shorthand. When set, configures (or creates) a <c>Working</c>-mode
    /// origin for this project. Use <see cref="GitRepos"/> for multi-origin setups.
    /// </summary>
    public string? GitUrl { get; set; }

    /// <summary>PAT or access token for authenticating with <see cref="GitUrl"/>.</summary>
    public string? GitToken { get; set; }

    /// <summary>Username used with <see cref="GitToken"/> for HTTP basic auth (defaults to "git").</summary>
    public string? GitUsername { get; set; }

    /// <summary>Default branch name for <see cref="GitUrl"/>. Defaults to "main".</summary>
    public string? DefaultBranch { get; set; }

    /// <summary>
    /// Full list of git origins for this project. When set, the applier reconciles all origins
    /// in this list (matched by <c>remoteUrl</c>) and removes any DB origins not present here.
    /// Takes precedence over the single-origin <see cref="GitUrl"/> shorthand.
    /// </summary>
    public List<GitRepoConfigModel>? GitRepos { get; set; }

    public bool? MountRepositoryInDocker { get; set; }
    public int? MaxConcurrentRunners { get; set; }
    public int? ConcurrentJobs { get; set; }
    public string? ActRunnerImage { get; set; }
    public string? ActEnv { get; set; }
    public string? ActSecrets { get; set; }
    public string? ActionCachePath { get; set; }
    public bool? UseNewActionCache { get; set; }
    public bool? ActionOfflineMode { get; set; }

    /// <summary>
    /// Newline-separated <c>owner/repo=localPath</c> mappings that redirect reusable workflow
    /// calls to local clones. Passed to <c>act</c> via <c>--local-repository</c> flags.
    /// </summary>
    public string? LocalRepositories { get; set; }

    public List<ProjectMemberConfigModel>? Members { get; set; }
}

/// <summary>JSON model for a single git origin within a project config file.</summary>
public class GitRepoConfigModel
{
    /// <summary>Remote git URL. Used as the unique key to match existing origins in the DB.</summary>
    public string RemoteUrl { get; set; } = string.Empty;

    /// <summary>PAT or access token for authenticating with <see cref="RemoteUrl"/>.</summary>
    public string? GitToken { get; set; }

    /// <summary>Username used with <see cref="GitToken"/> for HTTP basic auth (defaults to "git").</summary>
    public string? GitUsername { get; set; }

    /// <summary>Default branch name. Defaults to "main".</summary>
    public string? DefaultBranch { get; set; }

    /// <summary>How this origin is used: <c>Working</c> (agents push here), <c>Release</c>, or <c>ReadOnly</c>.</summary>
    public GitOriginMode Mode { get; set; } = GitOriginMode.Working;
}

/// <summary>JSON model for a project member entry in a config override file.</summary>
public class ProjectMemberConfigModel
{
    /// <summary>Optional user ID. Takes priority over <see cref="Username"/> when both are set.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Username of the member. Ignored when <see cref="UserId"/> is set.</summary>
    public string? Username { get; set; }

    public ProjectPermission Permissions { get; set; } = ProjectPermission.Read;
}
