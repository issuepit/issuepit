using IssuePit.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace IssuePit.Core.Data;

public class IssuePitDbContext(DbContextOptions<IssuePitDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueTask> IssueTasks => Set<IssueTask>();
    public DbSet<IssueAssignee> IssueAssignees => Set<IssueAssignee>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<McpServer> McpServers => Set<McpServer>();
    public DbSet<AgentMcpServer> AgentMcpServers => Set<AgentMcpServer>();
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
    public DbSet<CiCdRun> CiCdRuns => Set<CiCdRun>();
    public DbSet<CiCdRunLog> CiCdRunLogs => Set<CiCdRunLog>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<IssueComment> IssueComments => Set<IssueComment>();

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
    }
}
