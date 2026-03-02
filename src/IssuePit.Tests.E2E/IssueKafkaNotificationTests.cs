using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;

namespace IssuePit.Tests.E2E;

/// <summary>
/// Resource-dependent E2E tests that verify Kafka notifications are published
/// when an issue is created, using the real Aspire stack (Postgres, Kafka, Redis).
/// </summary>
[Collection("E2E")]
[Trait("Category", "E2E")]
public class IssueKafkaNotificationTests(AspireFixture fixture)
{
    /// <summary>
    /// Full E2E: create issue via API → consume from real Kafka topic and verify the notification payload.
    /// </summary>
    [Fact]
    public async Task CreateIssue_PublishesKafkaNotification_WithCorrectPayload()
    {
        var bootstrapServers = fixture.KafkaBootstrapServers
            ?? throw new InvalidOperationException("Kafka bootstrap servers not available from Aspire fixture.");

        var tenantId = await GetDefaultTenantIdAsync();
        using var client = CreateCookieClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);

        var username = $"e2e{Guid.NewGuid():N}"[..12];
        const string password = "TestPass1!";
        await client.PostAsJsonAsync("/api/auth/register", new { username, password });

        var orgSlug = $"e2e-kafka-{Guid.NewGuid():N}"[..16];
        var orgResp = await client.PostAsJsonAsync("/api/orgs", new { name = "Kafka E2E Org", slug = orgSlug });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<JsonElement>();
        var orgId = Guid.Parse(org.GetProperty("id").GetString()!);

        var projectSlug = $"e2e-kfk-{Guid.NewGuid():N}"[..14];
        var projResp = await client.PostAsJsonAsync("/api/projects",
            new { name = "Kafka E2E Project", slug = projectSlug, orgId });
        Assert.Equal(HttpStatusCode.Created, projResp.StatusCode);
        var project = await projResp.Content.ReadFromJsonAsync<JsonElement>();
        var projectId = Guid.Parse(project.GetProperty("id").GetString()!);

        // Start consuming BEFORE creating the issue to avoid a race condition.
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = $"e2e-test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetLogHandler((_, _) => { }) // suppress librdkafka noise in test output
            .Build();
        consumer.Subscribe("issue-assigned");

        // Wait for the consumer to receive its partition assignment before producing
        // any message. Without this, a message produced while the assignment is still
        // pending (rebalance in-flight) will have an offset earlier than the consumer's
        // "latest" start position and will be silently skipped.
        using var assignWait = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (consumer.Assignment.Count == 0 && !assignWait.Token.IsCancellationRequested)
        {
            consumer.Consume(TimeSpan.FromMilliseconds(200));
        }

        // Create the issue
        const string issueTitle = "E2E Kafka Notification Issue";
        var issueResp = await client.PostAsJsonAsync("/api/issues",
            new { title = issueTitle, projectId });
        Assert.Equal(HttpStatusCode.Created, issueResp.StatusCode);
        var issue = await issueResp.Content.ReadFromJsonAsync<JsonElement>();
        var issueId = issue.GetProperty("id").GetString()!;

        // Poll for the Kafka message with a timeout.
        // Swallow transient ConsumeException (e.g. "Unknown topic") during the first few polls
        // in case the consumer's metadata cache hasn't refreshed yet after topic creation.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ConsumeResult<string, string>? result = null;
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var msg = consumer.Consume(TimeSpan.FromSeconds(1));
                if (msg is null) continue;
                if (msg.Message.Key == issueId)
                {
                    result = msg;
                    break;
                }
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                // Transient — topic metadata may not be visible yet; retry.
            }
        }

        consumer.Close();

        Assert.NotNull(result);
        Assert.Equal("issue-assigned", result.Topic);

        var payload = JsonSerializer.Deserialize<JsonElement>(result.Message.Value);
        Assert.Equal(issueId, payload.GetProperty("Id").GetString());
        Assert.Equal(projectId.ToString(), payload.GetProperty("ProjectId").GetString());
        Assert.Equal(issueTitle, payload.GetProperty("Title").GetString());
        Assert.False(payload.TryGetProperty("AgentId", out _), "issue-assigned event produced on issue creation must not contain AgentId");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private HttpClient CreateCookieClient()
    {
        var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
        return new HttpClient(handler) { BaseAddress = fixture.ApiClient!.BaseAddress };
    }

    private async Task<string> GetDefaultTenantIdAsync()
    {
        var resp = await fixture.ApiClient!.GetAsync("/api/admin/tenants");
        resp.EnsureSuccessStatusCode();
        var tenants = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var tenant in tenants.EnumerateArray())
        {
            if (tenant.GetProperty("hostname").GetString() == "localhost")
                return tenant.GetProperty("id").GetString()!;
        }
        throw new InvalidOperationException("Default 'localhost' tenant not found. Ensure the migrator has run.");
    }
}
