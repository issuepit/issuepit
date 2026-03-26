using System.Xml;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Services;

/// <summary>
/// Parses JUnit XML test result files (e.g. produced by Vitest, Jest, pytest, NUnit) into
/// <see cref="CiCdTestSuite"/> and <see cref="CiCdTestCase"/> entities.
/// Supports both the single-suite (<c>&lt;testsuite&gt;</c>) and
/// multi-suite (<c>&lt;testsuites&gt;&lt;testsuite&gt;…&lt;/testsuites&gt;</c>) formats.
/// </summary>
public static class JUnitParser
{
    /// <summary>
    /// Parses a JUnit XML file and returns a populated <see cref="CiCdTestSuite"/>.
    /// The <see cref="CiCdTestSuite.CiCdRunId"/> must be set by the caller before persisting.
    /// Returns <c>null</c> if the file cannot be parsed as JUnit XML.
    /// </summary>
    public static CiCdTestSuite? Parse(string filePath)
    {
        try
        {
            var doc = new XmlDocument();
            doc.Load(filePath);
            return ParseDocument(doc, Path.GetFileNameWithoutExtension(filePath));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a JUnit XML stream and returns a populated <see cref="CiCdTestSuite"/>.
    /// The <see cref="CiCdTestSuite.CiCdRunId"/> must be set by the caller before persisting.
    /// Returns <c>null</c> if the stream cannot be parsed as JUnit XML.
    /// </summary>
    /// <param name="stream">Stream containing the JUnit XML content.</param>
    /// <param name="artifactName">Name to use as <see cref="CiCdTestSuite.ArtifactName"/>.</param>
    public static CiCdTestSuite? Parse(Stream stream, string artifactName)
    {
        try
        {
            var doc = new XmlDocument();
            doc.Load(stream);
            return ParseDocument(doc, artifactName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the XML document root element is <c>testsuite</c> or
    /// <c>testsuites</c>, indicating a JUnit-style test result file.
    /// </summary>
    public static bool IsJUnitDocument(XmlDocument doc)
    {
        var root = doc.DocumentElement?.LocalName;
        return string.Equals(root, "testsuite", StringComparison.OrdinalIgnoreCase)
            || string.Equals(root, "testsuites", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns <c>true</c> when the stream content looks like a JUnit XML file.
    /// The stream position is reset to its original position after detection.
    /// </summary>
    public static bool IsJUnitStream(Stream stream)
    {
        var originalPosition = stream.CanSeek ? stream.Position : 0L;
        try
        {
            var doc = new XmlDocument();
            doc.Load(stream);
            return IsJUnitDocument(doc);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (stream.CanSeek)
                stream.Position = originalPosition;
        }
    }

    /// <summary>Recursively finds all JUnit XML files (<c>.xml</c>) under <paramref name="rootPath"/>.</summary>
    public static IEnumerable<string> FindJUnitFiles(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return [];
        return Directory.EnumerateFiles(rootPath, "*.xml", SearchOption.AllDirectories);
    }

    private static CiCdTestSuite? ParseDocument(XmlDocument doc, string artifactName)
    {
        try
        {
            if (!IsJUnitDocument(doc))
                return null;

            var root = doc.DocumentElement!;

            // Collect all <testsuite> elements — either the root itself or children of <testsuites>.
            var suiteNodes = new List<XmlElement>();
            if (string.Equals(root.LocalName, "testsuites", StringComparison.OrdinalIgnoreCase))
            {
                foreach (XmlNode child in root.ChildNodes)
                {
                    if (child is XmlElement el && string.Equals(el.LocalName, "testsuite", StringComparison.OrdinalIgnoreCase))
                        suiteNodes.Add(el);
                }
            }
            else
            {
                // Root is a single <testsuite>.
                suiteNodes.Add(root);
            }

            if (suiteNodes.Count == 0)
                return null;

            // Aggregate counts and test cases across all suites.
            var total = 0;
            var passed = 0;
            var failed = 0;
            var skipped = 0;
            var durationMs = 0.0;
            var testCases = new List<CiCdTestCase>();

            // Try to parse wall-clock time from the first suite's timestamp for run-level duration.
            // If that is absent we sum individual test durations below.
            DateTime? suiteStart = null;
            if (suiteNodes.Count == 1)
            {
                var tsStr = suiteNodes[0].GetAttribute("timestamp");
                if (!string.IsNullOrWhiteSpace(tsStr) && DateTime.TryParse(tsStr, out var ts))
                    suiteStart = ts;
            }

            foreach (var suiteNode in suiteNodes)
            {
                var suiteTests = ParseAttrInt(suiteNode, "tests");
                var suiteFailed = ParseAttrInt(suiteNode, "failures") + ParseAttrInt(suiteNode, "errors");
                var suiteSkipped = ParseAttrInt(suiteNode, "skipped") + ParseAttrInt(suiteNode, "disabled");
                var suiteTime = ParseAttrDouble(suiteNode, "time") * 1000.0; // seconds → ms

                durationMs += suiteTime;
                total += suiteTests;
                failed += suiteFailed;
                skipped += suiteSkipped;

                foreach (XmlNode node in suiteNode.ChildNodes)
                {
                    if (node is not XmlElement tc ||
                        !string.Equals(tc.LocalName, "testcase", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var className = tc.GetAttribute("classname");
                    var methodName = tc.GetAttribute("name");
                    var timeSeconds = ParseAttrDouble(tc, "time");

                    var outcome = TestOutcome.Passed;
                    string? errorMessage = null;
                    string? stackTrace = null;

                    // Examine child elements to determine outcome.
                    foreach (XmlNode child in tc.ChildNodes)
                    {
                        if (child is not XmlElement childEl) continue;

                        if (string.Equals(childEl.LocalName, "failure", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(childEl.LocalName, "error", StringComparison.OrdinalIgnoreCase))
                        {
                            outcome = TestOutcome.Failed;
                            errorMessage = !string.IsNullOrWhiteSpace(childEl.GetAttribute("message"))
                                ? childEl.GetAttribute("message")
                                : null;
                            var content = childEl.InnerText?.Trim();
                            if (!string.IsNullOrWhiteSpace(content))
                                stackTrace = content;
                        }
                        else if (string.Equals(childEl.LocalName, "skipped", StringComparison.OrdinalIgnoreCase))
                        {
                            outcome = TestOutcome.Skipped;
                        }
                    }

                    if (outcome == TestOutcome.Passed)
                        passed++;

                    testCases.Add(new CiCdTestCase
                    {
                        Id = Guid.NewGuid(),
                        FullName = !string.IsNullOrWhiteSpace(className)
                            ? $"{className}.{methodName}"
                            : methodName,
                        ClassName = string.IsNullOrWhiteSpace(className) ? null : className,
                        MethodName = methodName,
                        Outcome = outcome,
                        DurationMs = timeSeconds * 1000.0,
                        ErrorMessage = errorMessage,
                        StackTrace = stackTrace,
                    });
                }
            }

            // When the suite attributes omit counts, derive them from parsed test cases.
            if (total == 0) total = testCases.Count;
            if (passed == 0 && total > 0)
                passed = testCases.Count(tc => tc.Outcome == TestOutcome.Passed);
            if (failed == 0)
                failed = testCases.Count(tc => tc.Outcome == TestOutcome.Failed);

            // Recalculate duration from individual tests when suite-level time is missing.
            if (durationMs == 0.0)
                durationMs = testCases.Sum(tc => tc.DurationMs);

            return new CiCdTestSuite
            {
                Id = Guid.NewGuid(),
                ArtifactName = artifactName,
                TotalTests = total,
                PassedTests = passed,
                FailedTests = failed,
                SkippedTests = skipped,
                DurationMs = durationMs,
                CreatedAt = DateTime.UtcNow,
                TestCases = testCases,
            };
        }
        catch
        {
            return null;
        }
    }

    private static int ParseAttrInt(XmlElement node, string attr)
    {
        var val = node.GetAttribute(attr);
        return int.TryParse(val, out var n) ? n : 0;
    }

    private static double ParseAttrDouble(XmlElement node, string attr)
    {
        var val = node.GetAttribute(attr);
        return double.TryParse(val, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.0;
    }
}
