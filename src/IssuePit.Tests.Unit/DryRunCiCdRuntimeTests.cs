using IssuePit.CiCdClient.Runtimes;
using IssuePit.CiCdClient.Services;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class DryRunCiCdRuntimeTests
{
    [Fact]
    public void WriteSimulatedArtifacts_NullPath_DoesNotThrow()
    {
        // Should silently do nothing when no artifact path is configured.
        DryRunCiCdRuntime.WriteSimulatedArtifacts(null);
    }

    [Fact]
    public void WriteSimulatedArtifacts_EmptyPath_DoesNotThrow()
    {
        DryRunCiCdRuntime.WriteSimulatedArtifacts(string.Empty);
    }

    [Fact]
    public void WriteSimulatedArtifacts_ValidPath_CreatesBuildOutputArtifact()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dry-run-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            DryRunCiCdRuntime.WriteSimulatedArtifacts(dir);

            // build-output artifact: <artifactDir>/build-output/1/output.txt
            var buildOutputFile = Path.Combine(dir, "build-output", "1", "output.txt");
            Assert.True(File.Exists(buildOutputFile), $"Expected build artifact at {buildOutputFile}");
            Assert.Contains("Build succeeded", File.ReadAllText(buildOutputFile));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteSimulatedArtifacts_ValidPath_CreatesTrxFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dry-run-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            DryRunCiCdRuntime.WriteSimulatedArtifacts(dir);

            // test-results artifact: <artifactDir>/test-results/1/results.trx
            var trxFile = Path.Combine(dir, "test-results", "1", "results.trx");
            Assert.True(File.Exists(trxFile), $"Expected TRX file at {trxFile}");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteSimulatedArtifacts_TrxFile_IsValidAndParseable()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dry-run-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            DryRunCiCdRuntime.WriteSimulatedArtifacts(dir);

            var trxFile = Path.Combine(dir, "test-results", "1", "results.trx");
            var suite = TrxParser.Parse(trxFile);

            Assert.NotNull(suite);
            Assert.Equal(1, suite.TotalTests);
            Assert.Equal(1, suite.PassedTests);
            Assert.Equal(0, suite.FailedTests);
            Assert.Single(suite.TestCases);
            Assert.Equal("DummyProject.DummyTests.DummyTest_Passes", suite.TestCases.First().FullName);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void WriteSimulatedArtifacts_TrxFilesFoundByFindTrxFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dry-run-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            DryRunCiCdRuntime.WriteSimulatedArtifacts(dir);

            var trxFiles = TrxParser.FindTrxFiles(dir).ToList();
            Assert.Single(trxFiles);
            Assert.EndsWith("results.trx", trxFiles[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DryRunTrxContent_IsValidXml()
    {
        // Ensure the embedded TRX string itself is valid and parseable.
        var path = Path.Combine(Path.GetTempPath(), $"dry-run-const-{Guid.NewGuid():N}.trx");
        File.WriteAllText(path, DryRunCiCdRuntime.DryRunTrxContent);
        try
        {
            var suite = TrxParser.Parse(path);
            Assert.NotNull(suite);
            Assert.Equal(1, suite.TotalTests);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
