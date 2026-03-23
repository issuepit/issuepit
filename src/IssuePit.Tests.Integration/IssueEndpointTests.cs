using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IssuePit.Tests.Integration;

[Trait("Category", "Integration")]
public class IssueEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid projectId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });

        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);

        var project = new Project { Id = Guid.NewGuid(), OrgId = org.Id, Name = "Proj", Slug = $"proj-{tenantId}" };
        db.Projects.Add(project);

        await db.SaveChangesAsync();
        return (tenantId, project.Id);
    }

    [Fact]
    public async Task CreateIssue_WithProjectIdOnly_Returns201()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/issues",
            new { title = "Test Issue", projectId, status = IssueStatus.Backlog, priority = IssuePriority.NoPriority, type = IssueType.Issue });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    /// <summary>
    /// Reproduces the kanban lane bug: issue creation was failing with 400 "The Project field is required"
    /// because the navigation property was non-nullable. Only projectId (not the full Project object) is sent.
    /// </summary>
    [Fact]
    public async Task CreateIssue_FromKanbanLane_WithStatusAndPriority_Returns201()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // This mirrors exactly what the kanban frontend sends via issueStore.createIssue
        var response = await _client.PostAsJsonAsync("/api/issues", new
        {
            title = "Kanban Issue",
            status = IssueStatus.InProgress,
            priority = IssuePriority.Medium,
            type = IssueType.Issue,
            projectId
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var issue = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Kanban Issue", issue.GetProperty("title").GetString());
        Assert.Equal(projectId.ToString(), issue.GetProperty("projectId").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task UpdateIssue_WithPartialPayload_ReturnsOkAndPreservesOtherFields()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Original Title", Body = "Original body", Number = 1 };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        // Update only the body — Title must not be required
        var response = await _client.PutAsJsonAsync($"/api/issues/{issue.Id}", new { body = "Updated body" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("Original Title", result.GetProperty("title").GetString());
        Assert.Equal("Updated body", result.GetProperty("body").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task GetIssues_ForProject_Returns200()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        db.Issues.Add(new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Existing Issue", Number = 1 });
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.GetAsync($"/api/issues?projectId={projectId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}

[Trait("Category", "Integration")]
public class IssueAssigneeKafkaTests(TrackingApiFactory factory) : IClassFixture<TrackingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid projectId, Guid orgId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });
        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);
        var project = new Project { Id = Guid.NewGuid(), OrgId = org.Id, Name = "Proj", Slug = $"proj-{tenantId}" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return (tenantId, project.Id, org.Id);
    }

    [Fact]
    public async Task AddAgentAssignee_PublishesIssueAssignedKafkaEvent()
    {
        var (tenantId, projectId, orgId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Agent Issue", Number = 1 };
        db.Issues.Add(issue);
        var agent = new Agent { Id = Guid.NewGuid(), OrgId = orgId, Name = "Bot", SystemPrompt = "sys", DockerImage = "img", AllowedTools = "[]", CreatedAt = DateTime.UtcNow };
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/issues/{issue.Id}/assignees", new { agentId = agent.Id });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var published = factory.Producer.Produced;
        var message = published.Single(m => m.Topic == "issue-assigned" && m.Message.Key == issue.Id.ToString());
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Message.Value);
        Assert.Equal(issue.Id.ToString(), payload.GetProperty("Id").GetString());
        Assert.Equal(agent.Id.ToString(), payload.GetProperty("AgentId").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task AddUserAssignee_DoesNotPublishKafkaEvent()
    {
        var (tenantId, projectId, _) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "User Issue", Number = 2 };
        db.Issues.Add(issue);
        var user = new User { Id = Guid.NewGuid(), TenantId = tenantId, Email = $"u-{Guid.NewGuid()}@test.com", Username = "testuser" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var before = factory.Producer.Produced.Count;

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/issues/{issue.Id}/assignees", new { userId = user.Id });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal(before, factory.Producer.Produced.Count);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}

[Trait("Category", "Integration")]
public class IssueCreationKafkaTests(TrackingApiFactory factory) : IClassFixture<TrackingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid projectId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });
        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);
        var project = new Project { Id = Guid.NewGuid(), OrgId = org.Id, Name = "Proj", Slug = $"proj-{tenantId}" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return (tenantId, project.Id);
    }

    [Fact]
    public async Task CreateIssue_PublishesIssueAssignedKafkaEvent()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/issues",
            new { title = "Kafka Test Issue", projectId, status = IssueStatus.Backlog, priority = IssuePriority.NoPriority, type = IssueType.Issue });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var issue = await response.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString();

        var published = factory.Producer.Produced;

        var message = published.Single(m => m.Topic == "issue-assigned" && m.Message.Key == issueId);
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Message.Value);
        Assert.Equal(issueId, payload.GetProperty("Id").GetString());
        Assert.Equal(projectId.ToString(), payload.GetProperty("ProjectId").GetString());
        Assert.Equal("Kafka Test Issue", payload.GetProperty("Title").GetString());

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task CreateIssue_KafkaPayload_DoesNotContainAgentId()
    {
        var (tenantId, projectId) = await SeedProjectAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync("/api/issues",
            new { title = "No Agent Issue", projectId, status = IssueStatus.Backlog, priority = IssuePriority.NoPriority, type = IssueType.Issue });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var issue = await response.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString();

        var message = factory.Producer.Produced.Single(m => m.Topic == "issue-assigned" && m.Message.Key == issueId);
        var payload = JsonSerializer.Deserialize<JsonElement>(message.Message.Value);
        Assert.False(payload.TryGetProperty("AgentId", out _), "Issue-created event must not contain AgentId");

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}

[Trait("Category", "Integration")]
public class IssueCommentMentionTests(TrackingApiFactory factory) : IClassFixture<TrackingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid tenantId, Guid projectId, Guid orgId)> SeedProjectAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"host-{tenantId}" });
        var org = new Organization { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Org", Slug = $"org-{tenantId}" };
        db.Organizations.Add(org);
        var project = new Project { Id = Guid.NewGuid(), OrgId = org.Id, Name = "Proj", Slug = $"proj-{tenantId}" };
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return (tenantId, project.Id, org.Id);
    }

    [Fact]
    public async Task AddComment_WithAgentMention_PublishesIssueAssignedEventWithTriggeringCommentId()
    {
        var (tenantId, projectId, orgId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Issue for mention test", Number = 1 };
        db.Issues.Add(issue);

        var agent = new Agent { Id = Guid.NewGuid(), OrgId = orgId, Name = "code-agent", SystemPrompt = "sys", DockerImage = "img", IsActive = true, AllowedTools = "[]", CreatedAt = DateTime.UtcNow };
        db.Agents.Add(agent);

        // Assign the agent so it's picked up when triggered
        db.IssueAssignees.Add(new IssueAssignee { Id = Guid.NewGuid(), IssueId = issue.Id, AgentId = agent.Id });

        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var beforeCount = factory.Producer.Produced.Count;
        var response = await _client.PostAsJsonAsync($"/api/issues/{issue.Id}/comments", new { body = "Please fix the bug @code-agent" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // A new issue-assigned event should have been published for the mentioned agent.
        var newMessages = factory.Producer.Produced.Skip(beforeCount).ToList();
        var matchingEvents = newMessages.Where(m => m.Topic == "issue-assigned" && m.Message.Key == issue.Id.ToString()).ToList();
        Assert.Single(matchingEvents);

        var payload = JsonSerializer.Deserialize<JsonElement>(matchingEvents[0].Message.Value);
        Assert.Equal(agent.Id.ToString(), payload.GetProperty("AgentId").GetString());

        // The triggering comment ID must be present in the payload so the prompt builder can mark
        // that comment as is_new="true".
        Assert.True(payload.TryGetProperty("TriggeringCommentId", out var tcProp));
        Assert.False(string.IsNullOrEmpty(tcProp.GetString()));

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task AddComment_WithUnknownAgentMention_DoesNotPublishEvent()
    {
        var (tenantId, projectId, _) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Issue no agent", Number = 2 };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var beforeCount = factory.Producer.Produced.Count;
        var response = await _client.PostAsJsonAsync($"/api/issues/{issue.Id}/comments", new { body = "@nonexistent-agent please help" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // No new issue-assigned events should have been published.
        var newMessages = factory.Producer.Produced.Skip(beforeCount)
            .Where(m => m.Topic == "issue-assigned")
            .ToList();
        Assert.Empty(newMessages);

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }

    [Fact]
    public async Task AddComment_WithAgentMention_AutoAssignsAgentIfNotAlreadyAssigned()
    {
        var (tenantId, projectId, orgId) = await SeedProjectAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IssuePitDbContext>();

        var issue = new Issue { Id = Guid.NewGuid(), ProjectId = projectId, Title = "Auto-assign test", Number = 3 };
        db.Issues.Add(issue);

        var agent = new Agent { Id = Guid.NewGuid(), OrgId = orgId, Name = "auto-agent", SystemPrompt = "sys", DockerImage = "img", IsActive = true, AllowedTools = "[]", CreatedAt = DateTime.UtcNow };
        db.Agents.Add(agent);
        // Do NOT assign the agent to the issue yet.

        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await _client.PostAsJsonAsync($"/api/issues/{issue.Id}/comments", new { body = "@auto-agent please implement this" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // The agent should have been auto-assigned.
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IssuePitDbContext>();
        var isAssigned = await verifyDb.IssueAssignees.AnyAsync(a => a.IssueId == issue.Id && a.AgentId == agent.Id);
        Assert.True(isAssigned, "Agent should have been auto-assigned after comment @mention");

        _client.DefaultRequestHeaders.Remove("X-Tenant-Id");
    }
}
