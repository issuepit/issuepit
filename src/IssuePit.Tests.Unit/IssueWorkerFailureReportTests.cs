using IssuePit.Core.Entities;
using IssuePit.ExecutionClient.Workers;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class IssueWorkerFailureReportTests
{
    [Fact]
    public void BuildFailedTestsXml_EmptyList_ReturnsEmptyTestsuites()
    {
        var xml = IssueWorker.BuildFailedTestsXml([]);
        Assert.Contains("<testsuites>", xml);
        Assert.Contains("</testsuites>", xml);
        Assert.DoesNotContain("<testsuite ", xml);
    }

    [Fact]
    public void BuildFailedTestsXml_SingleSuiteWithOneFailure_ProducesValidXml()
    {
        var suites = new List<FailedTestSuiteInfo>
        {
            new("unit-test-results", 10, 1,
            [
                new FailedTestCaseInfo(
                    "MyNamespace.MyClass.MyFailingTest",
                    "MyNamespace.MyClass",
                    "MyFailingTest",
                    123.456,
                    "Assert.Equal() Failure: expected 1 but was 2",
                    "at MyClass.MyFailingTest() in MyClass.cs:line 42"),
            ]),
        };

        var xml = IssueWorker.BuildFailedTestsXml(suites);

        Assert.Contains("<testsuites>", xml);
        Assert.Contains("name=\"unit-test-results\"", xml);
        Assert.Contains("tests=\"10\"", xml);
        Assert.Contains("failures=\"1\"", xml);
        Assert.Contains("name=\"MyFailingTest\"", xml);
        Assert.Contains("classname=\"MyNamespace.MyClass\"", xml);
        Assert.Contains("time=\"0.123\"", xml);
        Assert.Contains("Assert.Equal() Failure: expected 1 but was 2", xml);
        Assert.Contains("at MyClass.MyFailingTest() in MyClass.cs:line 42", xml);
        Assert.Contains("<failure", xml);
        Assert.Contains("</failure>", xml);
        Assert.Contains("</testcase>", xml);
        Assert.Contains("</testsuite>", xml);
        Assert.Contains("</testsuites>", xml);
    }

    [Fact]
    public void BuildFailedTestsXml_SpecialCharsInErrorMessage_AreEscaped()
    {
        var suites = new List<FailedTestSuiteInfo>
        {
            new("results", 1, 1,
            [
                new FailedTestCaseInfo(
                    "A.B.Test",
                    "A.B",
                    "Test",
                    10.0,
                    "Expected <foo> & \"bar\"",
                    null),
            ]),
        };

        var xml = IssueWorker.BuildFailedTestsXml(suites);

        Assert.Contains("Expected &lt;foo&gt; &amp; &quot;bar&quot;", xml);
    }

    [Fact]
    public void BuildFailedTestsXml_SpecialCharsInStackTrace_AreEscaped()
    {
        var suites = new List<FailedTestSuiteInfo>
        {
            new("results", 1, 1,
            [
                new FailedTestCaseInfo(
                    "A.B.Test",
                    "A.B",
                    "Test",
                    10.0,
                    "error",
                    "at A.B<T>.Test() where T : IFoo & IBar"),
            ]),
        };

        var xml = IssueWorker.BuildFailedTestsXml(suites);

        Assert.Contains("at A.B&lt;T&gt;.Test() where T : IFoo &amp; IBar", xml);
    }

    [Fact]
    public void BuildFailedTestsXml_NullClassName_OmitsClassnameAttribute()
    {
        var suites = new List<FailedTestSuiteInfo>
        {
            new("results", 1, 1,
            [
                new FailedTestCaseInfo("My.Test", null, "Test", 5.0, null, null),
            ]),
        };

        var xml = IssueWorker.BuildFailedTestsXml(suites);

        Assert.DoesNotContain("classname=", xml);
        Assert.Contains("name=\"Test\"", xml);
    }

    [Fact]
    public void BuildFailedTestsXml_NullMethodName_UsesFullName()
    {
        var suites = new List<FailedTestSuiteInfo>
        {
            new("results", 1, 1,
            [
                new FailedTestCaseInfo("My.Namespace.FullTest", "My.Namespace", null, 5.0, null, null),
            ]),
        };

        var xml = IssueWorker.BuildFailedTestsXml(suites);

        Assert.Contains("name=\"My.Namespace.FullTest\"", xml);
    }

    [Fact]
    public void BuildFailedTestsXml_MultipleSuites_AllIncluded()
    {
        // Scenario: multiple suites have failures — all are included in one XML document.
        // This covers the case where multiple jobs each produce test result artifacts.
        var suites = new List<FailedTestSuiteInfo>
        {
            new("suite-a", 5, 1, [new FailedTestCaseInfo("A.Test1", "A", "Test1", 1.0, "Suite A assertion failure", null)]),
            new("suite-b", 3, 2,
            [
                new FailedTestCaseInfo("B.Test1", "B", "Test1", 2.0, "Suite B first test error", null),
                new FailedTestCaseInfo("B.Test2", "B", "Test2", 3.0, "Suite B second test error", null),
            ]),
        };

        var xml = IssueWorker.BuildFailedTestsXml(suites);

        // Both suites are present with all their failing test cases.
        Assert.Contains("name=\"suite-a\"", xml);
        Assert.Contains("name=\"suite-b\"", xml);
        Assert.Equal(2, xml.Split("<testsuite ").Length - 1);
        Assert.Equal(3, xml.Split("<testcase ").Length - 1);
    }

    [Fact]
    public void BuildFailedTestsXml_NullStackTrace_OmitsStackTraceContent()
    {
        var suites = new List<FailedTestSuiteInfo>
        {
            new("results", 1, 1,
            [
                new FailedTestCaseInfo("A.Test", "A", "Test", 1.0, "error", null),
            ]),
        };

        var xml = IssueWorker.BuildFailedTestsXml(suites);

        // failure element should exist but contain no extra content lines
        Assert.Contains("<failure", xml);
        Assert.Contains("</failure>", xml);
    }

    [Fact]
    public void BuildFailedTestsXml_NullErrorMessage_UsesFallback()
    {
        var suites = new List<FailedTestSuiteInfo>
        {
            new("results", 1, 1,
            [
                new FailedTestCaseInfo("A.Test", "A", "Test", 1.0, null, null),
            ]),
        };

        var xml = IssueWorker.BuildFailedTestsXml(suites);

        Assert.Contains("message=\"Test failed\"", xml);
    }

    /// <summary>
    /// Verifies that <see cref="IssueWorker.BuildFailedTestsXml"/> produces one
    /// &lt;testsuite&gt; per suite, covering the "multiple jobs each with tests" scenario.
    /// </summary>
    [Fact]
    public void BuildFailedTestsXml_MultipleJobSuites_EachEmittedAsSeparateSuite()
    {
        var suites = new List<FailedTestSuiteInfo>
        {
            new("job-a-tests", 20, 2,
            [
                new FailedTestCaseInfo("A.Suite.Test1", "A.Suite", "Test1", 50.0, "Job A failure 1", null),
                new FailedTestCaseInfo("A.Suite.Test2", "A.Suite", "Test2", 60.0, "Job A failure 2", null),
            ]),
            new("job-b-tests", 15, 1,
            [
                new FailedTestCaseInfo("B.Suite.Test1", "B.Suite", "Test1", 30.0, "Job B failure", null),
            ]),
        };

        var xml = IssueWorker.BuildFailedTestsXml(suites);

        Assert.Contains("name=\"job-a-tests\"", xml);
        Assert.Contains("failures=\"2\"", xml);
        Assert.Contains("name=\"job-b-tests\"", xml);
        Assert.Contains("failures=\"1\"", xml);
        Assert.Equal(2, xml.Split("<testsuite ").Length - 1);
        Assert.Equal(3, xml.Split("<testcase ").Length - 1);
    }

    [Fact]
    public void BuildFixIssue_NoTestFailures_OmitsPhasedGuidance()
    {
        var original = new Issue { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Number = 42, Title = "Test Issue" };
        var fixIssue = IssueWorker.BuildFixIssue(original, "some failure logs", "main", failedTestCount: 0);

        Assert.DoesNotContain("Focus on fixing the first", fixIssue.Body);
        Assert.DoesNotContain("subsequent iterations", fixIssue.Body);
        Assert.Contains("some failure logs", fixIssue.Body);
    }

    [Fact]
    public void BuildFixIssue_FewTestFailures_OmitsPhasedGuidance()
    {
        var original = new Issue { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Number = 42, Title = "Test Issue" };
        var fixIssue = IssueWorker.BuildFixIssue(original, "some failure logs", "main", failedTestCount: 3);

        Assert.DoesNotContain("Focus on fixing the first", fixIssue.Body);
    }

    [Fact]
    public void BuildFixIssue_ManyTestFailures_IncludesPhasedGuidance()
    {
        var original = new Issue { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Number = 42, Title = "Test Issue" };
        var fixIssue = IssueWorker.BuildFixIssue(original, "test failure xml", "main", failedTestCount: 10);

        Assert.Contains($"Focus on fixing the first {IssueWorker.MaxTestsPerFixPhase} failures", fixIssue.Body);
        Assert.Contains("subsequent iterations", fixIssue.Body);
        Assert.Contains("test failure xml", fixIssue.Body);
    }

    [Fact]
    public void BuildFixIssue_PreservesOriginalFields()
    {
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var original = new Issue { Id = id, ProjectId = projectId, Number = 7, Title = "Original Title" };

        var fixIssue = IssueWorker.BuildFixIssue(original, "logs", "feature-branch");

        Assert.Equal(id, fixIssue.Id);
        Assert.Equal(projectId, fixIssue.ProjectId);
        Assert.Equal(7, fixIssue.Number);
        Assert.Equal("Fix CI/CD failures for: Original Title", fixIssue.Title);
        Assert.Equal("feature-branch", fixIssue.GitBranch);
        Assert.Contains("Do NOT run `git push`", fixIssue.Body);
    }

    [Fact]
    public void CiCdFailureReport_NeedsCondensing_FalseForTestResults()
    {
        var report = new CiCdFailureReport("<testsuites>...</testsuites>", NeedsCondensing: false, FailedTestCount: 2);

        Assert.False(report.NeedsCondensing);
        Assert.Equal(2, report.FailedTestCount);
    }

    [Fact]
    public void CiCdFailureReport_NeedsCondensing_TrueForRawLogs()
    {
        var report = new CiCdFailureReport("raw log output...", NeedsCondensing: true, FailedTestCount: 0);

        Assert.True(report.NeedsCondensing);
        Assert.Equal(0, report.FailedTestCount);
    }
}
