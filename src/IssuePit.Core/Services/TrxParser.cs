using System.Xml;
using IssuePit.Core.Entities;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Services;

/// <summary>
/// Parses Microsoft Visual Studio Test Results (<c>.trx</c>) XML files into
/// <see cref="CiCdTestSuite"/> and <see cref="CiCdTestCase"/> entities.
/// </summary>
public static class TrxParser
{
    private const string Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// Parses a <c>.trx</c> XML file and returns a populated <see cref="CiCdTestSuite"/>.
    /// The <see cref="CiCdTestSuite.CiCdRunId"/> must be set by the caller before persisting.
    /// Returns <c>null</c> if the file cannot be parsed.
    /// </summary>
    public static CiCdTestSuite? Parse(string trxFilePath)
    {
        try
        {
            var doc = new XmlDocument();
            doc.Load(trxFilePath);
            return ParseDocument(doc, Path.GetFileNameWithoutExtension(trxFilePath));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a <c>.trx</c> XML stream and returns a populated <see cref="CiCdTestSuite"/>.
    /// The <see cref="CiCdTestSuite.CiCdRunId"/> must be set by the caller before persisting.
    /// Returns <c>null</c> if the stream cannot be parsed.
    /// </summary>
    /// <param name="stream">Stream containing the TRX XML content.</param>
    /// <param name="artifactName">Name to use as <see cref="CiCdTestSuite.ArtifactName"/> (e.g. the artifact directory name).</param>
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

    private static CiCdTestSuite? ParseDocument(XmlDocument doc, string artifactName)
    {
        try
        {
            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("t", Ns);

            // --- Summary ---
            var countersNode = doc.SelectSingleNode("//t:Counters", nsmgr);
            var total = ParseAttrInt(countersNode, "total");
            var passed = ParseAttrInt(countersNode, "passed");
            var failed = ParseAttrInt(countersNode, "failed");
            // TRX does not have a dedicated "skipped" counter. "notExecuted" covers tests that were
            // excluded at runtime (e.g. [Ignore]) and "inconclusive" covers tests without assertions.
            // When total > passed + failed (some tests left unaccounted), treat the remainder as skipped.
            // Otherwise fall back to notExecuted + inconclusive as the skipped count.
            var notExecuted = ParseAttrInt(countersNode, "notExecuted") + ParseAttrInt(countersNode, "inconclusive");
            var remainder = total - passed - failed;
            var skipped = remainder > notExecuted ? remainder : notExecuted;

            // --- Duration from ResultSummary times or individual test times ---
            var durationMs = 0.0;
            var testRunNode = doc.SelectSingleNode("/t:TestRun", nsmgr);
            var startTimeStr = testRunNode?.Attributes?["start"]?.Value;
            var finishTimeStr = testRunNode?.Attributes?["finish"]?.Value;
            if (DateTime.TryParse(startTimeStr, out var start) && DateTime.TryParse(finishTimeStr, out var finish))
                durationMs = (finish - start).TotalMilliseconds;

            // --- Build test definitions map: testId → (className, methodName) ---
            var definitions = new Dictionary<string, (string? className, string? methodName)>();
            foreach (XmlNode def in doc.SelectNodes("//t:UnitTest", nsmgr) ?? EmptyNodeList.Instance)
            {
                var testId = def.Attributes?["id"]?.Value;
                if (string.IsNullOrWhiteSpace(testId)) continue;

                var methodNode = def.SelectSingleNode("t:TestMethod", nsmgr);
                var className = methodNode?.Attributes?["className"]?.Value;
                var methodName = methodNode?.Attributes?["name"]?.Value;
                definitions[testId] = (className, methodName);
            }

            // --- Parse individual test results ---
            var testCases = new List<CiCdTestCase>();
            foreach (XmlNode result in doc.SelectNodes("//t:UnitTestResult", nsmgr) ?? EmptyNodeList.Instance)
            {
                var testName = result.Attributes?["testName"]?.Value ?? string.Empty;
                var testId = result.Attributes?["testId"]?.Value ?? string.Empty;
                var outcomeStr = result.Attributes?["outcome"]?.Value ?? string.Empty;
                var durationStr = result.Attributes?["duration"]?.Value;

                definitions.TryGetValue(testId, out var def);

                var tc = new CiCdTestCase
                {
                    Id = Guid.NewGuid(),
                    FullName = !string.IsNullOrWhiteSpace(def.className)
                        ? $"{def.className}.{def.methodName ?? testName}"
                        : testName,
                    ClassName = def.className,
                    MethodName = def.methodName ?? testName,
                    Outcome = ParseOutcome(outcomeStr),
                    DurationMs = ParseDurationMs(durationStr),
                };

                // Extract error message and stack trace from <Output><ErrorInfo>
                var errorInfo = result.SelectSingleNode("t:Output/t:ErrorInfo", nsmgr);
                if (errorInfo is not null)
                {
                    tc.ErrorMessage = errorInfo.SelectSingleNode("t:Message", nsmgr)?.InnerText?.Trim();
                    tc.StackTrace = errorInfo.SelectSingleNode("t:StackTrace", nsmgr)?.InnerText?.Trim();
                }

                testCases.Add(tc);
            }

            // Recalculate duration from individual tests when the run-level times are missing.
            if (durationMs == 0.0)
                durationMs = testCases.Sum(tc => tc.DurationMs);

            var suite = new CiCdTestSuite
            {
                Id = Guid.NewGuid(),
                ArtifactName = artifactName,
                TotalTests = total > 0 ? total : testCases.Count,
                PassedTests = passed,
                FailedTests = failed,
                SkippedTests = skipped,
                DurationMs = durationMs,
                CreatedAt = DateTime.UtcNow,
                TestCases = testCases,
            };

            return suite;
        }
        catch
        {
            return null;
        }
    }

    private static TestOutcome ParseOutcome(string outcome) =>
        outcome.ToLowerInvariant() switch
        {
            "passed" => TestOutcome.Passed,
            "failed" => TestOutcome.Failed,
            "notexecuted" or "inconclusive" or "aborted" => TestOutcome.NotExecuted,
            "skipped" or "ignored" or "pending" => TestOutcome.Skipped,
            _ => TestOutcome.NotExecuted,
        };

    private static int ParseAttrInt(XmlNode? node, string attr)
    {
        var val = node?.Attributes?[attr]?.Value;
        return int.TryParse(val, out var n) ? n : 0;
    }

    private static double ParseDurationMs(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return 0.0;
        if (TimeSpan.TryParse(duration, out var ts))
            return ts.TotalMilliseconds;
        return 0.0;
    }

    /// <summary>Recursively finds all <c>.trx</c> files under <paramref name="rootPath"/>.</summary>
    public static IEnumerable<string> FindTrxFiles(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return [];
        return Directory.EnumerateFiles(rootPath, "*.trx", SearchOption.AllDirectories);
    }

    // Helper: XmlNodeList that is empty, used when SelectNodes returns null.
    private sealed class EmptyNodeList : XmlNodeList
    {
        public static readonly EmptyNodeList Instance = new();
        public override int Count => 0;
        public override XmlNode Item(int index) => null!;
        public override System.Collections.IEnumerator GetEnumerator() =>
            System.Linq.Enumerable.Empty<XmlNode>().GetEnumerator();
    }
}
