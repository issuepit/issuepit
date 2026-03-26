using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Core.Data;

public class IssuePitDbContext(DbContextOptions<IssuePitDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueLink> IssueLinks => Set<IssueLink>();
    public DbSet<IssueTask> IssueTasks => Set<IssueTask>();
    public DbSet<IssueAssignee> IssueAssignees => Set<IssueAssignee>();
    public DbSet<IssueComment> IssueComments => Set<IssueComment>();
    public DbSet<IssueAttachment> IssueAttachments => Set<IssueAttachment>();
    public DbSet<CodeReviewComment> CodeReviewComments => Set<CodeReviewComment>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<McpServer> McpServers => Set<McpServer>();
    public DbSet<AgentMcpServer> AgentMcpServers => Set<AgentMcpServer>();
    public DbSet<AgentProject> AgentProjects => Set<AgentProject>();
    public DbSet<AgentOrg> AgentOrgs => Set<AgentOrg>();
    public DbSet<McpServerSecret> McpServerSecrets => Set<McpServerSecret>();
    public DbSet<McpServerProject> McpServerProjects => Set<McpServerProject>();
    public DbSet<McpServerProjectAgent> McpServerProjectAgents => Set<McpServerProjectAgent>();
    public DbSet<KanbanBoard> KanbanBoards => Set<KanbanBoard>();
    public DbSet<KanbanColumn> KanbanColumns => Set<KanbanColumn>();
    public DbSet<KanbanTransition> KanbanTransitions => Set<KanbanTransition>();
    public DbSet<User> Users => Set<User>();
    public DbSet<GitHubIdentity> GitHubIdentities => Set<GitHubIdentity>();
    public DbSet<GitHubIdentityProject> GitHubIdentityProjects => Set<GitHubIdentityProject>();
    public DbSet<GitHubIdentityOrg> GitHubIdentityOrgs => Set<GitHubIdentityOrg>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<RuntimeConfiguration> RuntimeConfigurations => Set<RuntimeConfiguration>();
    public DbSet<AgentSession> AgentSessions => Set<AgentSession>();
    public DbSet<AgentSessionLog> AgentSessionLogs => Set<AgentSessionLog>();
    public DbSet<AgentSessionMessage> AgentSessionMessages => Set<AgentSessionMessage>();
    public DbSet<AgentAuth> AgentAuths => Set<AgentAuth>();
    public DbSet<CiCdRun> CiCdRuns => Set<CiCdRun>();
    public DbSet<CiCdRunLog> CiCdRunLogs => Set<CiCdRunLog>();
    public DbSet<CiCdTestSuite> CiCdTestSuites => Set<CiCdTestSuite>();
    public DbSet<CiCdTestCase> CiCdTestCases => Set<CiCdTestCase>();
    public DbSet<CiCdArtifact> CiCdArtifacts => Set<CiCdArtifact>();
    public DbSet<CiCdCoverageReport> CiCdCoverageReports => Set<CiCdCoverageReport>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<GitRepository> GitRepositories => Set<GitRepository>();
    public DbSet<TelegramBot> TelegramBots => Set<TelegramBot>();
    public DbSet<ProjectMetricSnapshot> ProjectMetricSnapshots => Set<ProjectMetricSnapshot>();
    public DbSet<IssueEvent> IssueEvents => Set<IssueEvent>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<MergeRequest> MergeRequests => Set<MergeRequest>();
    public DbSet<TodoBoard> TodoBoards => Set<TodoBoard>();
    public DbSet<TodoCategory> TodoCategories => Set<TodoCategory>();
    public DbSet<Todo> Todos => Set<Todo>();
    public DbSet<TodoBoardMembership> TodoBoardMemberships => Set<TodoBoardMembership>();
    public DbSet<TodoCategoryMembership> TodoCategoryMemberships => Set<TodoCategoryMembership>();
    public DbSet<GitHubSyncConfig> GitHubSyncConfigs => Set<GitHubSyncConfig>();
    public DbSet<GitHubSyncRun> GitHubSyncRuns => Set<GitHubSyncRun>();
    public DbSet<GitHubSyncRunLog> GitHubSyncRunLogs => Set<GitHubSyncRunLog>();
    public DbSet<JiraSyncConfig> JiraSyncConfigs => Set<JiraSyncConfig>();
    public DbSet<JiraSyncRun> JiraSyncRuns => Set<JiraSyncRun>();
    public DbSet<JiraSyncRunLog> JiraSyncRunLogs => Set<JiraSyncRunLog>();
    public DbSet<IssueGitMapping> IssueGitMappings => Set<IssueGitMapping>();
    public DbSet<BranchDetectionRun> BranchDetectionRuns => Set<BranchDetectionRun>();
    public DbSet<BranchDetectionRunLog> BranchDetectionRunLogs => Set<BranchDetectionRunLog>();
    public DbSet<ConfigRepoSyncRun> ConfigRepoSyncRuns => Set<ConfigRepoSyncRun>();
    public DbSet<ConfigRepoSyncRunLog> ConfigRepoSyncRunLogs => Set<ConfigRepoSyncRunLog>();
    public DbSet<ProjectProperty> ProjectProperties => Set<ProjectProperty>();
    public DbSet<IssuePropertyValue> IssuePropertyValues => Set<IssuePropertyValue>();
    public DbSet<McpToken> McpTokens => Set<McpToken>();
    public DbSet<DashboardLayout> DashboardLayouts => Set<DashboardLayout>();
    public DbSet<SimilarIssueRun> SimilarIssueRuns => Set<SimilarIssueRun>();
    public DbSet<SimilarIssueRunLog> SimilarIssueRunLogs => Set<SimilarIssueRunLog>();
    public DbSet<SimilarIssuePair> SimilarIssuePairs => Set<SimilarIssuePair>();
    public DbSet<GitServerRepo> GitServerRepos => Set<GitServerRepo>();
    public DbSet<GitServerPermission> GitServerPermissions => Set<GitServerPermission>();
    public DbSet<GitServerBranchProtection> GitServerBranchProtections => Set<GitServerBranchProtection>();
    public DbSet<GitPat> GitPats => Set<GitPat>();
    public DbSet<IssueExternalSource> IssueExternalSources => Set<IssueExternalSource>();
    public DbSet<PinnedProject> PinnedProjects => Set<PinnedProject>();
    public DbSet<HetznerServer> HetznerServers => Set<HetznerServer>();
    public DbSet<HetznerServerRuntimeHistory> HetznerServerRuntimeHistories => Set<HetznerServerRuntimeHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AgentMcpServer>()
            .HasKey(x => new { x.AgentId, x.McpServerId });

        modelBuilder.Entity<AgentMcpServer>()
            .HasOne(x => x.Agent)
            .WithMany(a => a.AgentMcpServers)
            .HasForeignKey(x => x.AgentId);

        modelBuilder.Entity<AgentMcpServer>()
            .HasOne(x => x.McpServer)
            .WithMany(m => m.AgentMcpServers)
            .HasForeignKey(x => x.McpServerId);

        modelBuilder.Entity<Agent>()
            .HasOne(a => a.ParentAgent)
            .WithMany(a => a.ChildAgents)
            .HasForeignKey(a => a.ParentAgentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AgentProject>()
            .HasKey(x => new { x.AgentId, x.ProjectId });

        modelBuilder.Entity<AgentProject>()
            .HasOne(x => x.Agent)
            .WithMany(a => a.AgentProjects)
            .HasForeignKey(x => x.AgentId);

        modelBuilder.Entity<AgentProject>()
            .HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId);

        modelBuilder.Entity<AgentOrg>()
            .HasKey(x => new { x.AgentId, x.OrgId });

        modelBuilder.Entity<AgentOrg>()
            .HasOne(x => x.Agent)
            .WithMany(a => a.AgentOrgs)
            .HasForeignKey(x => x.AgentId);

        modelBuilder.Entity<AgentOrg>()
            .HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrgId);

        modelBuilder.Entity<McpServerProjectAgent>()
            .HasKey(x => new { x.McpServerId, x.ProjectId, x.AgentId });

        modelBuilder.Entity<McpServerProjectAgent>()
            .HasOne(x => x.McpServer)
            .WithMany()
            .HasForeignKey(x => x.McpServerId);

        modelBuilder.Entity<McpServerProjectAgent>()
            .HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId);

        modelBuilder.Entity<McpServerProjectAgent>()
            .HasOne(x => x.Agent)
            .WithMany()
            .HasForeignKey(x => x.AgentId);

        modelBuilder.Entity<McpServerProject>()
            .HasKey(x => new { x.McpServerId, x.ProjectId });

        modelBuilder.Entity<McpServerProject>()
            .HasOne(x => x.McpServer)
            .WithMany(m => m.McpServerProjects)
            .HasForeignKey(x => x.McpServerId);

        modelBuilder.Entity<McpServerProject>()
            .HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId);

        modelBuilder.Entity<IssueAssignee>()
            .HasOne(x => x.Issue)
            .WithMany(i => i.Assignees)
            .HasForeignKey(x => x.IssueId);

        modelBuilder.Entity<IssueAssignee>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .IsRequired(false);

        modelBuilder.Entity<IssueAssignee>()
            .HasOne(x => x.Agent)
            .WithMany()
            .HasForeignKey(x => x.AgentId)
            .IsRequired(false);

        modelBuilder.Entity<Issue>()
            .HasOne(i => i.ParentIssue)
            .WithMany(i => i.SubIssues)
            .HasForeignKey(i => i.ParentIssueId)
            .IsRequired(false);

        modelBuilder.Entity<Issue>()
            .HasMany(i => i.Links)
            .WithOne(l => l.Issue)
            .HasForeignKey(l => l.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IssueEvent>()
            .HasOne(e => e.Issue)
            .WithMany()
            .HasForeignKey(e => e.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IssueEvent>()
            .HasOne(e => e.ActorUser)
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .IsRequired(false);

        modelBuilder.Entity<IssueEvent>()
            .HasOne(e => e.ActorAgent)
            .WithMany()
            .HasForeignKey(e => e.ActorAgentId)
            .IsRequired(false);

        modelBuilder.Entity<IssueLink>()
            .HasOne(l => l.TargetIssue)
            .WithMany()
            .HasForeignKey(l => l.TargetIssueId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Issue>()
            .HasMany(i => i.Labels)
            .WithMany()
            .UsingEntity("issue_labels");

        modelBuilder.Entity<TeamMember>()
            .HasKey(x => new { x.TeamId, x.UserId });

        modelBuilder.Entity<TeamMember>()
            .HasOne(x => x.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(x => x.TeamId);

        modelBuilder.Entity<TeamMember>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<OrganizationMember>()
            .HasKey(x => new { x.OrgId, x.UserId });

        modelBuilder.Entity<OrganizationMember>()
            .HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrgId);

        modelBuilder.Entity<OrganizationMember>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<ProjectMember>()
            .HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId);

        modelBuilder.Entity<ProjectMember>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .IsRequired(false);

        modelBuilder.Entity<ProjectMember>()
            .HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .IsRequired(false);

        modelBuilder.Entity<GitHubIdentityProject>()
            .HasKey(x => new { x.GitHubIdentityId, x.ProjectId });

        modelBuilder.Entity<GitHubIdentityProject>()
            .HasOne(x => x.GitHubIdentity)
            .WithMany(g => g.Projects)
            .HasForeignKey(x => x.GitHubIdentityId);

        modelBuilder.Entity<GitHubIdentityProject>()
            .HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId);

        modelBuilder.Entity<GitHubIdentityOrg>()
            .HasKey(x => new { x.GitHubIdentityId, x.OrgId });

        modelBuilder.Entity<GitHubIdentityOrg>()
            .HasOne(x => x.GitHubIdentity)
            .WithMany(g => g.Orgs)
            .HasForeignKey(x => x.GitHubIdentityId);

        modelBuilder.Entity<GitHubIdentityOrg>()
            .HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrgId);

        modelBuilder.Entity<TodoBoardMembership>()
            .HasKey(x => new { x.TodoId, x.BoardId });

        modelBuilder.Entity<TodoBoardMembership>()
            .HasOne(x => x.Todo)
            .WithMany(t => t.BoardMemberships)
            .HasForeignKey(x => x.TodoId);

        modelBuilder.Entity<TodoBoardMembership>()
            .HasOne(x => x.Board)
            .WithMany()
            .HasForeignKey(x => x.BoardId);

        modelBuilder.Entity<TodoCategoryMembership>()
            .HasKey(x => new { x.TodoId, x.CategoryId });

        modelBuilder.Entity<TodoCategoryMembership>()
            .HasOne(x => x.Todo)
            .WithMany(t => t.CategoryMemberships)
            .HasForeignKey(x => x.TodoId);

        modelBuilder.Entity<TodoCategoryMembership>()
            .HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId);

        modelBuilder.Entity<IssueAttachment>()
            .HasOne(a => a.Issue)
            .WithMany()
            .HasForeignKey(a => a.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IssueAttachment>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .IsRequired(false);

        modelBuilder.Entity<McpToken>()
            .HasIndex(t => t.KeyHash)
            .IsUnique();

        modelBuilder.Entity<SimilarIssuePair>()
            .HasOne(p => p.Issue)
            .WithMany()
            .HasForeignKey(p => p.IssueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SimilarIssuePair>()
            .HasOne(p => p.SimilarIssue)
            .WithMany()
            .HasForeignKey(p => p.SimilarIssueId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PinnedProject>()
            .HasIndex(pp => new { pp.UserId, pp.ProjectId })
            .IsUnique();
    }
}
