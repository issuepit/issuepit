using IssuePit.Core.Enums;
using IssuePit.Core.Services;

namespace IssuePit.Tests.Unit;

public class JUnitParserTests
{
    private const string SimpleJUnit = """
        <?xml version="1.0" encoding="UTF-8"?>
        <testsuites name="test suite" tests="3" failures="1" errors="0" skipped="1" time="1.234">
          <testsuite name="MyTestSuite" tests="3" failures="1" errors="0" skipped="1" time="1.234" timestamp="2024-01-01T10:00:00">
            <testcase classname="MyTestSuite" name="test_passes" time="1.000" />
            <testcase classname="MyTestSuite" name="test_fails" time="0.200">
              <failure message="Expected 1 but got 2" type="AssertionError">stack trace line 1&#xA;stack trace line 2</failure>
            </testcase>
            <testcase classname="MyTestSuite" name="test_skipped" time="0.034">
              <skipped />
            </testcase>
          </testsuite>
        </testsuites>
        """;

    private const string SingleSuiteJUnit = """
        <?xml version="1.0" encoding="UTF-8"?>
        <testsuite name="MySuite" tests="2" failures="0" errors="0" skipped="0" time="0.5">
          <testcase classname="MySuite" name="test_a" time="0.3" />
          <testcase classname="MySuite" name="test_b" time="0.2" />
        </testsuite>
        """;

    private static string CreateXmlFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parse_ValidJUnit_ReturnsSuite()
    {
        var path = CreateXmlFile(SimpleJUnit);
        try
        {
            var suite = JUnitParser.Parse(path);
            Assert.NotNull(suite);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidJUnit_CorrectCounts()
    {
        var path = CreateXmlFile(SimpleJUnit);
        try
        {
            var suite = JUnitParser.Parse(path)!;
            Assert.Equal(3, suite.TotalTests);
            Assert.Equal(1, suite.PassedTests);
            Assert.Equal(1, suite.FailedTests);
            Assert.Equal(1, suite.SkippedTests);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidJUnit_HasTestCases()
    {
        var path = CreateXmlFile(SimpleJUnit);
        try
        {
            var suite = JUnitParser.Parse(path)!;
            Assert.Equal(3, suite.TestCases.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidJUnit_PassedTestHasCorrectOutcome()
    {
        var path = CreateXmlFile(SimpleJUnit);
        try
        {
            var suite = JUnitParser.Parse(path)!;
            var passed = suite.TestCases.First(tc => tc.MethodName == "test_passes");
            Assert.Equal(TestOutcome.Passed, passed.Outcome);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidJUnit_FailedTestHasErrorMessage()
    {
        var path = CreateXmlFile(SimpleJUnit);
        try
        {
            var suite = JUnitParser.Parse(path)!;
            var failed = suite.TestCases.First(tc => tc.MethodName == "test_fails");
            Assert.Equal(TestOutcome.Failed, failed.Outcome);
            Assert.Equal("Expected 1 but got 2", failed.ErrorMessage);
            Assert.NotNull(failed.StackTrace);
            Assert.Contains("stack trace", failed.StackTrace);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidJUnit_SkippedTestHasCorrectOutcome()
    {
        var path = CreateXmlFile(SimpleJUnit);
        try
        {
            var suite = JUnitParser.Parse(path)!;
            var skipped = suite.TestCases.First(tc => tc.MethodName == "test_skipped");
            Assert.Equal(TestOutcome.Skipped, skipped.Outcome);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidJUnit_DurationCalculatedFromSuiteTime()
    {
        var path = CreateXmlFile(SimpleJUnit);
        try
        {
            var suite = JUnitParser.Parse(path)!;
            // time="1.234" seconds → 1234 ms
            Assert.Equal(1234.0, suite.DurationMs, precision: 0);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidJUnit_FullNameIncludesClassName()
    {
        var path = CreateXmlFile(SimpleJUnit);
        try
        {
            var suite = JUnitParser.Parse(path)!;
            var tc = suite.TestCases.First(tc => tc.MethodName == "test_passes");
            Assert.Equal("MyTestSuite.test_passes", tc.FullName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_SingleSuiteElement_ReturnsSuite()
    {
        var path = CreateXmlFile(SingleSuiteJUnit);
        try
        {
            var suite = JUnitParser.Parse(path)!;
            Assert.NotNull(suite);
            Assert.Equal(2, suite.TotalTests);
            Assert.Equal(2, suite.PassedTests);
            Assert.Equal(0, suite.FailedTests);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_InvalidXml_ReturnsNull()
    {
        var path = CreateXmlFile("<not valid xml <<");
        try
        {
            var suite = JUnitParser.Parse(path);
            Assert.Null(suite);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_NonExistentFile_ReturnsNull()
    {
        var suite = JUnitParser.Parse("/tmp/this-file-does-not-exist-abc123.xml");
        Assert.Null(suite);
    }

    [Fact]
    public void Parse_NonJUnitXml_ReturnsNull()
    {
        // An XML file with a non-JUnit root element should return null.
        var path = CreateXmlFile("<coverage line-rate=\"0.8\" />");
        try
        {
            var suite = JUnitParser.Parse(path);
            Assert.Null(suite);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_FromStream_ReturnsSuiteWithArtifactName()
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(SimpleJUnit));
        var suite = JUnitParser.Parse(ms, "unit-test-results");
        Assert.NotNull(suite);
        Assert.Equal("unit-test-results", suite!.ArtifactName);
        Assert.Equal(3, suite.TotalTests);
        Assert.Equal(1, suite.PassedTests);
        Assert.Equal(1, suite.FailedTests);
        Assert.Equal(1, suite.SkippedTests);
    }

    [Fact]
    public void Parse_FromStream_InvalidXml_ReturnsNull()
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<not valid xml <<"));
        var suite = JUnitParser.Parse(ms, "some-artifact");
        Assert.Null(suite);
    }

    [Fact]
    public void IsJUnitStream_ValidJUnit_ReturnsTrue()
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(SimpleJUnit));
        Assert.True(JUnitParser.IsJUnitStream(ms));
    }

    [Fact]
    public void IsJUnitStream_CoberturaXml_ReturnsFalse()
    {
        const string cobertura = """<?xml version="1.0"?><coverage line-rate="0.8" />""";
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(cobertura));
        Assert.False(JUnitParser.IsJUnitStream(ms));
    }

    [Fact]
    public void FindJUnitFiles_NonExistentDirectory_ReturnsEmpty()
    {
        var files = JUnitParser.FindJUnitFiles("/tmp/no-such-dir-abc123xyz");
        Assert.Empty(files);
    }

    [Fact]
    public void FindJUnitFiles_EmptyDirectory_ReturnsEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"junit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var files = JUnitParser.FindJUnitFiles(dir);
            Assert.Empty(files);
        }
        finally { Directory.Delete(dir); }
    }

    [Fact]
    public void FindJUnitFiles_DirectoryWithXml_ReturnsXmlFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"junit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var xmlPath = Path.Combine(dir, "junit.xml");
        File.WriteAllText(xmlPath, "<test/>");
        try
        {
            var files = JUnitParser.FindJUnitFiles(dir).ToList();
            Assert.Single(files);
            Assert.Equal(xmlPath, files[0]);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
