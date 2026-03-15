using IssuePit.CiCdClient.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class ArtifactStorageServiceTests
{
    private static ArtifactStorageService CreateService()
    {
        var config = new ConfigurationBuilder().Build();
        return new ArtifactStorageService(config, NullLogger<ArtifactStorageService>.Instance);
    }

    private static ArtifactStorageService CreateServiceWithUrl(string serviceUrl)
    {
        // ASP.NET Core env vars use __ as separator which maps to : in the config system.
        // The configuration key must use : (colon) separator, not __ (double-underscore).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ImageStorage:ServiceUrl"] = serviceUrl })
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
    public void IsConfigured_WithServiceUrl_ReturnsTrue()
    {
        // Verifies the colon-separator config key is used (not the double-underscore env-var name).
        var svc = CreateServiceWithUrl("http://localhost:4566");
        Assert.True(svc.IsConfigured);
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

    [Fact]
    public void CountArtifactFiles_ExtensionlessZipFile_CountsEntriesInZip()
    {
        // Simulate act v7+ direct-upload: artifact stored without .zip extension but IS a zip archive.
        var dir = Path.Combine(Path.GetTempPath(), $"count-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        // Create an extensionless file that is actually a zip containing 2 entries.
        var zipPath = Path.Combine(dir, "test-results-trx");
        using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            zip.CreateEntry("a.txt").Open().Close();
            zip.CreateEntry("b.txt").Open().Close();
        }

        try
        {
            var (count, _) = ArtifactStorageService.CountArtifactFiles(dir);
            // Should count the entries inside the zip (2), not the zip file itself (1).
            Assert.Equal(2, count);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void CountArtifactFiles_ExtensionlessRawFile_CountsAsOneFile()
    {
        // Simulate act v7+ direct-upload: artifact stored without extension, NOT a zip (raw content).
        var dir = Path.Combine(Path.GetTempPath(), $"count-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        // Create an extensionless file with raw (non-zip) content.
        var rawPath = Path.Combine(dir, "my-artifact");
        File.WriteAllText(rawPath, "raw content, not a zip");

        try
        {
            var (count, size) = ArtifactStorageService.CountArtifactFiles(dir);
            // Not a zip — should count as 1 raw file.
            Assert.Equal(1, count);
            Assert.True(size > 0);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
