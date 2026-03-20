using IssuePit.Core.Services;

namespace IssuePit.Tests.Unit;

[Trait("Category", "Unit")]
public class CoberturaParserTests
{
    private const string SimpleCobertura = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.85" branch-rate="0.72" version="1.9" timestamp="1700000000"
                  lines-covered="170" lines-valid="200" branches-covered="36" branches-valid="50">
          <sources>
            <source>.</source>
          </sources>
          <packages>
            <package name="DummyProject" line-rate="0.85" branch-rate="0.72" complexity="10">
              <classes>
                <class name="DummyProject.DummyClass" filename="DummyClass.cs" line-rate="0.85" branch-rate="0.72">
                  <methods />
                </class>
              </classes>
            </package>
          </packages>
        </coverage>
        """;

    private const string CoberturaRatesOnly = """
        <?xml version="1.0" encoding="utf-8"?>
        <coverage line-rate="0.90" branch-rate="0.80" version="1.9" timestamp="1700000000">
          <sources><source>.</source></sources>
          <packages />
        </coverage>
        """;

    private static string CreateXmlFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Parse_ValidCobertura_ReturnsReport()
    {
        var path = CreateXmlFile(SimpleCobertura);
        try
        {
            var report = CoberturaParser.Parse(path);
            Assert.NotNull(report);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidCobertura_CorrectRates()
    {
        var path = CreateXmlFile(SimpleCobertura);
        try
        {
            var report = CoberturaParser.Parse(path)!;
            Assert.Equal(0.85, report.LineRate, precision: 4);
            Assert.Equal(0.72, report.BranchRate, precision: 4);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidCobertura_CorrectCounts()
    {
        var path = CreateXmlFile(SimpleCobertura);
        try
        {
            var report = CoberturaParser.Parse(path)!;
            Assert.Equal(170, report.LinesCovered);
            Assert.Equal(200, report.LinesValid);
            Assert.Equal(36, report.BranchesCovered);
            Assert.Equal(50, report.BranchesValid);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_ValidCobertura_ArtifactNameFromFileName()
    {
        var path = CreateXmlFile(SimpleCobertura);
        try
        {
            var report = CoberturaParser.Parse(path)!;
            // ArtifactName should be the file name without extension.
            Assert.Equal(Path.GetFileNameWithoutExtension(path), report.ArtifactName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_RatesOnly_NoCounts_ReturnsReport()
    {
        var path = CreateXmlFile(CoberturaRatesOnly);
        try
        {
            var report = CoberturaParser.Parse(path)!;
            Assert.NotNull(report);
            Assert.Equal(0.90, report.LineRate, precision: 4);
            Assert.Equal(0.80, report.BranchRate, precision: 4);
            Assert.Equal(0, report.LinesValid);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Parse_FromStream_ReturnsReportWithArtifactName()
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(SimpleCobertura));
        var report = CoberturaParser.Parse(ms, "unit-coverage");
        Assert.NotNull(report);
        Assert.Equal("unit-coverage", report!.ArtifactName);
        Assert.Equal(0.85, report.LineRate, precision: 4);
    }

    [Fact]
    public void Parse_InvalidXml_ReturnsNull()
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<not valid xml <<"));
        var report = CoberturaParser.Parse(ms, "bad");
        Assert.Null(report);
    }

    [Fact]
    public void Parse_NonCoberturaXml_ReturnsNull()
    {
        var xml = """
            <?xml version="1.0"?>
            <configuration>
              <appSettings>
                <add key="foo" value="bar" />
              </appSettings>
            </configuration>
            """;
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var report = CoberturaParser.Parse(ms, "config");
        Assert.Null(report);
    }

    [Fact]
    public void LooksLikeCoberturaFile_CoberturaXml_ReturnsTrue()
    {
        Assert.True(CoberturaParser.LooksLikeCoberturaFile("coverage.cobertura.xml"));
        Assert.True(CoberturaParser.LooksLikeCoberturaFile("cobertura.xml"));
        Assert.True(CoberturaParser.LooksLikeCoberturaFile("coverage.xml"));
        Assert.True(CoberturaParser.LooksLikeCoberturaFile("/path/to/unit-coverage.xml"));
    }

    [Fact]
    public void LooksLikeCoberturaFile_NonCoverage_ReturnsFalse()
    {
        Assert.False(CoberturaParser.LooksLikeCoberturaFile("results.trx"));
        Assert.False(CoberturaParser.LooksLikeCoberturaFile("config.xml"));
        Assert.False(CoberturaParser.LooksLikeCoberturaFile("appsettings.xml"));
    }

    [Fact]
    public void FindCoberturaFiles_FindsMatchingFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cobertura-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "coverage.cobertura.xml"), SimpleCobertura);
            File.WriteAllText(Path.Combine(dir, "results.trx"), "<xml/>");
            File.WriteAllText(Path.Combine(dir, "config.xml"), "<configuration/>");

            var found = CoberturaParser.FindCoberturaFiles(dir).ToList();
            Assert.Single(found);
            Assert.Contains("coverage.cobertura.xml", found[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
