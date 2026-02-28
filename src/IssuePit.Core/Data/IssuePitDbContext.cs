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
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<RuntimeConfiguration> RuntimeConfigurations => Set<RuntimeConfiguration>();
    public DbSet<AgentSession> AgentSessions => Set<AgentSession>();
    public DbSet<CiCdRun> CiCdRuns => Set<CiCdRun>();
    public DbSet<CiCdRunLog> CiCdRunLogs => Set<CiCdRunLog>();
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
    }
}
