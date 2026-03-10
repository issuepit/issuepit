using IssuePit.CiCdClient.Services;
using IssuePit.Core.Enums;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class TrxParserTests
{
    private static string CreateTrxFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.trx");
        File.WriteAllText(path, content);
        return path;
    }

    private const string SimpleTrx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <TestRun id="abc" name="TestRun" start="2024-01-01T10:00:00" finish="2024-01-01T10:00:05" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions>
            <UnitTest name="MyTest" id="test-1">
              <TestMethod className="MyNamespace.MyClass" name="MyTest" />
            </UnitTest>
            <UnitTest name="MyFailingTest" id="test-2">
              <TestMethod className="MyNamespace.MyClass" name="MyFailingTest" />
            </UnitTest>
          </TestDefinitions>
          <Results>
            <UnitTestResult testId="test-1" testName="MyTest" outcome="Passed" duration="00:00:00.1234567" />
            <UnitTestResult testId="test-2" testName="MyFailingTest" outcome="Failed" duration="00:00:00.0500000">
              <Output>
                <ErrorInfo>
                  <Message>Assert.Equal() Failure</Message>
                  <StackTrace>at MyClass.MyFailingTest() in MyClass.cs:line 10</StackTrace>
                </ErrorInfo>
              </Output>
            </UnitTestResult>
          </Results>
          <ResultSummary outcome="Failed">
            <Counters total="2" executed="2" passed="1" failed="1" error="0" />
          </ResultSummary>
        </TestRun>
        """;

    [Fact]
    public void Parse_ValidTrx_ReturnsSuite()
    {
        var path = CreateTrxFile(SimpleTrx);
        try
        {
            var suite = TrxParser.Parse(path);
            Assert.NotNull(suite);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidTrx_CorrectCounts()
    {
        var path = CreateTrxFile(SimpleTrx);
        try
        {
            var suite = TrxParser.Parse(path)!;
            Assert.Equal(2, suite.TotalTests);
            Assert.Equal(1, suite.PassedTests);
            Assert.Equal(1, suite.FailedTests);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidTrx_HasTestCases()
    {
        var path = CreateTrxFile(SimpleTrx);
        try
        {
            var suite = TrxParser.Parse(path)!;
            Assert.Equal(2, suite.TestCases.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidTrx_PassedTestHasCorrectOutcome()
    {
        var path = CreateTrxFile(SimpleTrx);
        try
        {
            var suite = TrxParser.Parse(path)!;
            var passed = suite.TestCases.First(tc => tc.MethodName == "MyTest");
            Assert.Equal(TestOutcome.Passed, passed.Outcome);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidTrx_FailedTestHasErrorMessage()
    {
        var path = CreateTrxFile(SimpleTrx);
        try
        {
            var suite = TrxParser.Parse(path)!;
            var failed = suite.TestCases.First(tc => tc.MethodName == "MyFailingTest");
            Assert.Equal(TestOutcome.Failed, failed.Outcome);
            Assert.Contains("Assert.Equal()", failed.ErrorMessage);
            Assert.NotNull(failed.StackTrace);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidTrx_DurationCalculatedFromRunTimes()
    {
        var path = CreateTrxFile(SimpleTrx);
        try
        {
            var suite = TrxParser.Parse(path)!;
            // start="2024-01-01T10:00:00" finish="2024-01-01T10:00:05" → 5000 ms
            Assert.Equal(5000.0, suite.DurationMs, precision: 0);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidTrx_FullNameIncludesClassName()
    {
        var path = CreateTrxFile(SimpleTrx);
        try
        {
            var suite = TrxParser.Parse(path)!;
            var tc = suite.TestCases.First(tc => tc.MethodName == "MyTest");
            Assert.Equal("MyNamespace.MyClass.MyTest", tc.FullName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_InvalidXml_ReturnsNull()
    {
        var path = CreateTrxFile("<not valid xml <<");
        try
        {
            var suite = TrxParser.Parse(path);
            Assert.Null(suite);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_NonExistentFile_ReturnsNull()
    {
        var suite = TrxParser.Parse("/tmp/this-file-does-not-exist-abc123.trx");
        Assert.Null(suite);
    }

    [Fact]
    public void FindTrxFiles_NonExistentDirectory_ReturnsEmpty()
    {
        var files = TrxParser.FindTrxFiles("/tmp/no-such-dir-abc123xyz");
        Assert.Empty(files);
    }

    [Fact]
    public void FindTrxFiles_EmptyDirectory_ReturnsEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"trx-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var files = TrxParser.FindTrxFiles(dir);
            Assert.Empty(files);
        }
        finally { Directory.Delete(dir); }
    }

    [Fact]
    public void FindTrxFiles_DirectoryWithTrx_ReturnsTrxFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"trx-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var trxPath = Path.Combine(dir, "results.trx");
        File.WriteAllText(trxPath, "<test/>");
        try
        {
            var files = TrxParser.FindTrxFiles(dir).ToList();
            Assert.Single(files);
            Assert.Equal(trxPath, files[0]);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void BuildActArgumentsList_WithArtifactServerPath_EmitsFlag()
    {
        var trigger = new IssuePit.CiCdClient.Runtimes.TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null,
            Branch: null,
            Workflow: null,
            AgentSessionId: null,
            WorkspacePath: null,
            EventName: null,
            ArtifactServerPath: "/tmp/artifacts");
        var args = IssuePit.CiCdClient.Runtimes.NativeCiCdRuntime.BuildActArgumentsList(trigger).ToList();
        var idx = args.IndexOf("--artifact-server-path");
        Assert.True(idx >= 0, "--artifact-server-path flag should be present");
        Assert.Equal("/tmp/artifacts", args[idx + 1]);
    }

    [Fact]
    public void BuildActArgumentsList_NoArtifactServerPath_FlagAbsent()
    {
        var trigger = new IssuePit.CiCdClient.Runtimes.TriggerPayload(
            ProjectId: Guid.NewGuid(),
            CommitSha: null,
            Branch: null,
            Workflow: null,
            AgentSessionId: null,
            WorkspacePath: null,
            EventName: null);
        var args = IssuePit.CiCdClient.Runtimes.NativeCiCdRuntime.BuildActArgumentsList(trigger);
        Assert.DoesNotContain("--artifact-server-path", args);
    }

    [Fact]
    public void Parse_FromStream_ReturnsSuiteWithArtifactName()
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(SimpleTrx));
        var suite = TrxParser.Parse(ms, "unit-test-results");
        Assert.NotNull(suite);
        Assert.Equal("unit-test-results", suite!.ArtifactName);
        Assert.Equal(2, suite.TotalTests);
        Assert.Equal(1, suite.PassedTests);
        Assert.Equal(1, suite.FailedTests);
    }

    [Fact]
    public void Parse_FromStream_InvalidXml_ReturnsNull()
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<not valid xml <<"));
        var suite = TrxParser.Parse(ms, "some-artifact");
        Assert.Null(suite);
    }

    [Fact]
    public void Parse_TrxInsideZip_ReturnsCorrectSuite()
    {
        // Simulate the act artifact server structure:
        // artifactDir/<runNumber>/<artifactName>/<trxFile>.zip  (contains <trxFile>.trx)
        var artifactDir = Path.Combine(Path.GetTempPath(), $"trx-zip-test-{Guid.NewGuid():N}");
        var artifactSubDir = Path.Combine(artifactDir, "1", "unit-test-results");
        Directory.CreateDirectory(artifactSubDir);

        // Create a zip containing a .trx file (act artifact server format).
        var zipPath = Path.Combine(artifactSubDir, "TestResults.trx.zip");
        using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("TestResults.trx");
            using var entryStream = entry.Open();
            using var writer = new System.IO.StreamWriter(entryStream);
            writer.Write(SimpleTrx);
        }

        try
        {
            // The TrxParser.FindTrxFiles should NOT find the .trx (it's inside a zip).
            var bareTrx = TrxParser.FindTrxFiles(artifactDir).ToList();
            Assert.Empty(bareTrx);

            // But TrxParser.Parse(stream, name) should parse it correctly after extraction.
            using var zip2 = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var entry = zip2.Entries.First(e => e.FullName.EndsWith(".trx", StringComparison.OrdinalIgnoreCase));
            using var stream = entry.Open();
            var suite = TrxParser.Parse(stream, "unit-test-results");
            Assert.NotNull(suite);
            Assert.Equal("unit-test-results", suite!.ArtifactName);
            Assert.Equal(2, suite.TotalTests);
        }
        finally
        {
            Directory.Delete(artifactDir, recursive: true);
        }
    }
}
