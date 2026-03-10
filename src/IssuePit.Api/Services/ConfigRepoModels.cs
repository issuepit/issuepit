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

    /// <summary>Remote git URL for this project's repository (cloned for code access and CI/CD).</summary>
    public string? GitUrl { get; set; }

    /// <summary>PAT or access token for authenticating with <see cref="GitUrl"/>.</summary>
    public string? GitToken { get; set; }

    /// <summary>Username used with <see cref="GitToken"/> for HTTP basic auth (defaults to "git").</summary>
    public string? GitUsername { get; set; }

    /// <summary>Default branch name. Defaults to "main".</summary>
    public string? DefaultBranch { get; set; }

    public bool? MountRepositoryInDocker { get; set; }
    public int? MaxConcurrentRunners { get; set; }
    public int? ConcurrentJobs { get; set; }
    public string? ActRunnerImage { get; set; }
    public string? ActEnv { get; set; }
    public string? ActSecrets { get; set; }
    public string? ActionCachePath { get; set; }
    public bool? UseNewActionCache { get; set; }
    public bool? ActionOfflineMode { get; set; }
    public string? LocalRepositories { get; set; }
    public List<ProjectMemberConfigModel>? Members { get; set; }
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
