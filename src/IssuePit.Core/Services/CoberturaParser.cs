using System.Xml;
using IssuePit.Core.Entities;

namespace IssuePit.Core.Services;

/// <summary>
/// Parses Cobertura XML coverage report files into <see cref="CiCdCoverageReport"/> entities.
/// </summary>
public static class CoberturaParser
{
    /// <summary>
    /// Parses a Cobertura XML file and returns a populated <see cref="CiCdCoverageReport"/>.
    /// The <see cref="CiCdCoverageReport.CiCdRunId"/> must be set by the caller before persisting.
    /// Returns <c>null</c> if the file cannot be parsed.
    /// </summary>
    public static CiCdCoverageReport? Parse(string filePath)
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
    /// Parses a Cobertura XML stream and returns a populated <see cref="CiCdCoverageReport"/>.
    /// The <see cref="CiCdCoverageReport.CiCdRunId"/> must be set by the caller before persisting.
    /// Returns <c>null</c> if the stream cannot be parsed.
    /// </summary>
    /// <param name="stream">Stream containing the Cobertura XML content.</param>
    /// <param name="artifactName">Name to use as <see cref="CiCdCoverageReport.ArtifactName"/> (e.g. the artifact directory name).</param>
    public static CiCdCoverageReport? Parse(Stream stream, string artifactName)
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

    private static CiCdCoverageReport? ParseDocument(XmlDocument doc, string artifactName)
    {
        try
        {
            // Cobertura root element is <coverage ...>
            var coverageNode = doc.SelectSingleNode("/coverage") ?? doc.DocumentElement;
            if (coverageNode is null)
                return null;

            // Validate this is actually a Cobertura file by checking for expected attributes.
            var lineRate = ParseAttrDouble(coverageNode, "line-rate");
            var branchRate = ParseAttrDouble(coverageNode, "branch-rate");

            // At least one of the key attributes must be present for this to be a valid Cobertura file.
            if (coverageNode.Attributes?["line-rate"] is null && coverageNode.Attributes?["branch-rate"] is null)
                return null;

            var linesCovered = ParseAttrInt(coverageNode, "lines-covered");
            var linesValid = ParseAttrInt(coverageNode, "lines-valid");
            var branchesCovered = ParseAttrInt(coverageNode, "branches-covered");
            var branchesValid = ParseAttrInt(coverageNode, "branches-valid");

            // If covered/valid counts are missing, try to derive them from the rate.
            // Some Cobertura variants only emit rates without absolute counts.
            if (linesValid == 0 && lineRate > 0)
            {
                // Cannot infer absolute numbers from rate alone — leave as 0.
            }

            return new CiCdCoverageReport
            {
                Id = Guid.NewGuid(),
                ArtifactName = artifactName,
                LineRate = lineRate,
                BranchRate = branchRate,
                LinesCovered = linesCovered,
                LinesValid = linesValid,
                BranchesCovered = branchesCovered,
                BranchesValid = branchesValid,
                CreatedAt = DateTime.UtcNow,
            };
        }
        catch
        {
            return null;
        }
    }

    private static double ParseAttrDouble(XmlNode? node, string attr)
    {
        var val = node?.Attributes?[attr]?.Value;
        return double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.0;
    }

    private static int ParseAttrInt(XmlNode? node, string attr)
    {
        var val = node?.Attributes?[attr]?.Value;
        return int.TryParse(val, out var n) ? n : 0;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="fileName"/> looks like a Cobertura report.
    /// Cobertura files are typically named <c>coverage.xml</c>, <c>coverage.cobertura.xml</c>,
    /// <c>cobertura.xml</c>, or anything matching <c>*coverage*.xml</c>.
    /// </summary>
    public static bool LooksLikeCoberturaFile(string fileName)
    {
        var name = Path.GetFileName(fileName).ToLowerInvariant();
        return name.EndsWith(".xml") && (
            name.Contains("cobertura") ||
            name.Contains("coverage") ||
            name == "coverage.xml");
    }

    /// <summary>Recursively finds all files that look like Cobertura coverage reports under <paramref name="rootPath"/>.</summary>
    public static IEnumerable<string> FindCoberturaFiles(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return [];
        return Directory.EnumerateFiles(rootPath, "*.xml", SearchOption.AllDirectories)
            .Where(LooksLikeCoberturaFile);
    }
}
