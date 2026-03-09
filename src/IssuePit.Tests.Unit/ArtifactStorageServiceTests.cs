using IssuePit.CiCdClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class ArtifactStorageServiceTests
{
    private static ArtifactStorageService CreateService(string? localStorePath = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(localStorePath is not null
                ? new Dictionary<string, string?> { ["CiCd__LocalArtifactStorePath"] = localStorePath }
                : [])
            .Build();
        return new ArtifactStorageService(config, NullLogger<ArtifactStorageService>.Instance);
    }

    [Fact]
    public void IsConfigured_WithoutServiceUrl_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public async Task SaveLocallyAsync_CreatesZipWithArtifactContent()
    {
        var storeDir = Path.Combine(Path.GetTempPath(), $"artifact-store-test-{Guid.NewGuid():N}");
        var artifactDir = Path.Combine(Path.GetTempPath(), $"artifact-src-{Guid.NewGuid():N}");

        // Create a fake artifact directory with one text file (simulating a raw file, not a zip).
        Directory.CreateDirectory(artifactDir);
        File.WriteAllText(Path.Combine(artifactDir, "output.txt"), "hello world");

        var svc = CreateService(storeDir);
        var runId = Guid.NewGuid();
        try
        {
            var (url, key) = await svc.SaveLocallyAsync(artifactDir, "my-artifact", runId);

            Assert.Null(url);
            Assert.NotNull(key);
            Assert.StartsWith("local:", key, StringComparison.Ordinal);

            // Verify the ZIP was created at the expected path.
            var relativePath = key!["local:".Length..];
            var fullPath = Path.Combine(storeDir, relativePath);
            Assert.True(File.Exists(fullPath), $"Expected ZIP at {fullPath}");

            // Verify the ZIP contains the artifact file.
            using var zip = System.IO.Compression.ZipFile.OpenRead(fullPath);
            Assert.Contains(zip.Entries, e => e.Name == "output.txt");
        }
        finally
        {
            if (Directory.Exists(storeDir)) Directory.Delete(storeDir, recursive: true);
            if (Directory.Exists(artifactDir)) Directory.Delete(artifactDir, recursive: true);
        }
    }

    [Fact]
    public async Task SaveLocallyAsync_ArtifactContainingZip_UnpacksInnerZip()
    {
        var storeDir = Path.Combine(Path.GetTempPath(), $"artifact-store-test-{Guid.NewGuid():N}");
        var artifactDir = Path.Combine(Path.GetTempPath(), $"artifact-src-{Guid.NewGuid():N}");

        // Create an artifact directory with an inner zip (act artifact server format).
        Directory.CreateDirectory(artifactDir);
        var innerZipPath = Path.Combine(artifactDir, "results.trx.zip");
        using (var innerZip = System.IO.Compression.ZipFile.Open(innerZipPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = innerZip.CreateEntry("results.trx");
            using var s = entry.Open();
            using var w = new StreamWriter(s);
            w.Write("<TestRun />");
        }

        var svc = CreateService(storeDir);
        var runId = Guid.NewGuid();
        try
        {
            var (_, key) = await svc.SaveLocallyAsync(artifactDir, "test-results", runId);
            Assert.NotNull(key);

            var relativePath = key!["local:".Length..];
            var fullPath = Path.Combine(storeDir, relativePath);

            // The outer ZIP should contain the extracted inner entry (results.trx), not the inner zip.
            using var zip = System.IO.Compression.ZipFile.OpenRead(fullPath);
            Assert.Contains(zip.Entries, e => e.Name == "results.trx");
            Assert.DoesNotContain(zip.Entries, e => e.Name == "results.trx.zip");
        }
        finally
        {
            if (Directory.Exists(storeDir)) Directory.Delete(storeDir, recursive: true);
            if (Directory.Exists(artifactDir)) Directory.Delete(artifactDir, recursive: true);
        }
    }

    [Fact]
    public void CountArtifactFiles_PlainFile_CountsCorrectly()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"count-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(dir, "b.txt"), "world");
        try
        {
            var (count, size) = ArtifactStorageService.CountArtifactFiles(dir);
            Assert.Equal(2, count);
            Assert.True(size > 0);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}