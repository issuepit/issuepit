using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using IssuePit.Api.Services;
using IssuePit.Core.Data;
using IssuePit.Core.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class GitHubActionsArtifactServiceTests
{
    // Minimal TRX content with one passing test.
    private const string MinimalTrx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun id="run1" name="TestRun" start="2024-01-01T10:00:00" finish="2024-01-01T10:00:05"
                 xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions>
            <UnitTest name="DummyTest_Passes" id="t1">
              <TestMethod className="DummyProject.DummyTests" name="DummyTest_Passes" />
            </UnitTest>
          </TestDefinitions>
          <Results>
            <UnitTestResult testId="t1" testName="DummyTest_Passes" outcome="Passed" duration="00:00:00.050" />
          </Results>
          <ResultSummary outcome="Passed">
            <Counters total="1" executed="1" passed="1" failed="0" error="0" />
          </ResultSummary>
        </TestRun>
        """;

    /// <summary>Creates an in-memory <see cref="IssuePitDbContext"/> with a unique database name.</summary>
    private static IssuePitDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<IssuePitDbContext>()
            .UseInMemoryDatabase($"ghtest-{Guid.NewGuid():N}")
            .Options;
        return new IssuePitDbContext(opts);
    }

    /// <summary>
    /// Builds a ZIP archive in memory containing one <c>.trx</c> file with the given content.
    /// </summary>
    private static byte[] BuildTrxZip(string trxContent)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("TestResults/test-results.trx");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(trxContent);
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Creates a fake <see cref="IHttpClientFactory"/> whose created clients use
    /// <paramref name="handler"/> for all requests.
    /// </summary>
    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
        => new FakeHttpClientFactory(handler);

    [Fact]
    public async Task ProcessArtifactsAsync_NoGitHubConfig_SkipsGracefully()
    {
        await using var db = CreateDb();

        // Seed: project with no GitHub sync config
        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"h-{tenantId}" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "O", Slug = $"o-{orgId}" });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "P", Slug = $"p-{projectId}" });
        db.CiCdRuns.Add(new CiCdRun { Id = runId, ProjectId = projectId, CommitSha = "abc", ExternalSource = "github", ExternalRunId = "1" });
        await db.SaveChangesAsync();

        var svc = new GitHubActionsArtifactService(
            db,
            new NoOpDataProtectionProvider(),
            CreateFactory(new FakeHandler(_ => throw new InvalidOperationException("Should not be called"))),
            NullLogger<GitHubActionsArtifactService>.Instance);

        // Should complete without throwing.
        await svc.ProcessArtifactsAsync(runId, projectId, "1");

        Assert.Empty(db.CiCdTestSuites.Where(s => s.CiCdRunId == runId));
    }

    // Helper: encodes a plain-text token the same way the real data-protection Protect(string)
    // extension does (Base64Url of UTF-8 bytes). The IdentityDataProtectionProvider returns
    // bytes as-is, so the Unprotect(string) extension decodes from Base64Url and then
    // UTF-8 decodes the bytes to recover the original token string.
    private static string EncodeToken(string plainToken)
    {
        var bytes = Encoding.UTF8.GetBytes(plainToken);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    [Fact]
    public async Task ProcessArtifactsAsync_WithTrxArtifact_StoresTestResults()
    {
        await using var db = CreateDb();

        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"h-{tenantId}" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "O", Slug = $"o-{orgId}" });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "P", Slug = $"p-{projectId}" });
        db.CiCdRuns.Add(new CiCdRun { Id = runId, ProjectId = projectId, CommitSha = "abc", ExternalSource = "github", ExternalRunId = "42" });

        // Add a GitHub identity with a plain-text token (no encryption in tests).
        var identityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, TenantId = tenantId, Email = "u@test.com", Username = "u" });
        db.GitHubIdentities.Add(new GitHubIdentity
        {
            Id = identityId,
            UserId = userId,
            EncryptedToken = EncodeToken("fake-token"),
            GitHubUsername = "testuser",
            Name = "Test",
        });
        db.GitHubSyncConfigs.Add(new GitHubSyncConfig
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            GitHubIdentityId = identityId,
            GitHubRepo = "testowner/testrepo",
        });
        await db.SaveChangesAsync();

        // Prepare fake GitHub API responses.
        var artifactId = 999L;
        var listResponse = JsonSerializer.Serialize(new
        {
            total_count = 1,
            artifacts = new[]
            {
                new { id = artifactId, name = "test-results", archive_download_url = "https://api.github.com/fake", expired = false }
            }
        });
        var zipBytes = BuildTrxZip(MinimalTrx);

        var handler = new FakeHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/runs/42/artifacts"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(listResponse, Encoding.UTF8, "application/json")
                };

            if (request.RequestUri.AbsolutePath.Contains($"/artifacts/{artifactId}/zip"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(zipBytes)
                };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        // Use a no-op data-protection provider that returns the token as-is.
        var svc = new GitHubActionsArtifactService(
            db,
            new IdentityDataProtectionProvider(),
            CreateFactory(handler),
            NullLogger<GitHubActionsArtifactService>.Instance);

        await svc.ProcessArtifactsAsync(runId, projectId, "42");

        // Verify test results were stored.
        var suites = await db.CiCdTestSuites
            .Include(s => s.TestCases)
            .Where(s => s.CiCdRunId == runId)
            .ToListAsync();

        Assert.Single(suites);
        Assert.Equal("test-results", suites[0].ArtifactName);
        Assert.Equal(1, suites[0].TotalTests);
        Assert.Equal(1, suites[0].PassedTests);
        Assert.Equal(0, suites[0].FailedTests);

        Assert.Single(suites[0].TestCases);
        Assert.Equal("DummyTest_Passes", suites[0].TestCases.First().MethodName);
    }

    [Fact]
    public async Task ProcessArtifactsAsync_NoArtifacts_StoresNothing()
    {
        await using var db = CreateDb();

        var tenantId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Hostname = $"h-{tenantId}" });
        db.Organizations.Add(new Organization { Id = orgId, TenantId = tenantId, Name = "O", Slug = $"o-{orgId}" });
        db.Projects.Add(new Project { Id = projectId, OrgId = orgId, Name = "P", Slug = $"p-{projectId}" });
        db.CiCdRuns.Add(new CiCdRun { Id = runId, ProjectId = projectId, CommitSha = "abc", ExternalSource = "github", ExternalRunId = "7" });

        var identityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, TenantId = tenantId, Email = "u2@test.com", Username = "u2" });
        db.GitHubIdentities.Add(new GitHubIdentity
        {
            Id = identityId, UserId = userId, EncryptedToken = EncodeToken("tok"), GitHubUsername = "u2", Name = "U2",
        });
        db.GitHubSyncConfigs.Add(new GitHubSyncConfig
        {
            Id = Guid.NewGuid(), ProjectId = projectId, GitHubIdentityId = identityId, GitHubRepo = "o/r",
        });
        await db.SaveChangesAsync();

        var emptyList = JsonSerializer.Serialize(new { total_count = 0, artifacts = Array.Empty<object>() });
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(emptyList, Encoding.UTF8, "application/json")
        });

        var svc = new GitHubActionsArtifactService(
            db,
            new IdentityDataProtectionProvider(),
            CreateFactory(handler),
            NullLogger<GitHubActionsArtifactService>.Instance);

        await svc.ProcessArtifactsAsync(runId, projectId, "7");

        Assert.Empty(db.CiCdTestSuites.Where(s => s.CiCdRunId == runId));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test doubles
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(respond(request));
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            var client = new HttpClient(handler);
            return client;
        }
    }

    /// <summary>
    /// A data-protection provider that returns the plain-text input unchanged.
    /// Used so tests do not need real key material.
    /// </summary>
    private sealed class IdentityDataProtectionProvider : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose) => new IdentityProtector();

        private sealed class IdentityProtector : IDataProtector
        {
            public IDataProtector CreateProtector(string purpose) => new IdentityProtector();
            public byte[] Protect(byte[] plaintext) => plaintext;
            public byte[] Unprotect(byte[] protectedData) => protectedData;
        }
    }

    /// <summary>
    /// A data-protection provider whose Unprotect always throws.
    /// Used to simulate a missing/invalid GitHub token.
    /// </summary>
    private sealed class NoOpDataProtectionProvider : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose) => new ThrowingProtector();

        private sealed class ThrowingProtector : IDataProtector
        {
            public IDataProtector CreateProtector(string purpose) => new ThrowingProtector();
            public byte[] Protect(byte[] plaintext) => plaintext;
            public byte[] Unprotect(byte[] protectedData) => throw new InvalidOperationException("No key");
        }
    }
}
