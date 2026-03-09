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

    [Fact]
    public void IsConfigured_WithoutServiceUrl_ReturnsFalse()
    {
        var svc = CreateService();
        Assert.False(svc.IsConfigured);
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
