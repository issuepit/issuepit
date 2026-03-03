using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IssuePit.Core.Enums;

namespace IssuePit.Core.Entities;

/// <summary>Represents a single test case result within a <see cref="CiCdTestSuite"/>.</summary>
[Table("cicd_test_cases")]
public class CiCdTestCase
{
    [Key]
    public Guid Id { get; set; }

    public Guid CiCdTestSuiteId { get; set; }

    [ForeignKey(nameof(CiCdTestSuiteId))]
    public CiCdTestSuite CiCdTestSuite { get; set; } = null!;

    /// <summary>Fully-qualified test name including class and method (e.g. "MyNamespace.MyClass.MyTest").</summary>
    [MaxLength(1000)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ClassName { get; set; }

    [MaxLength(500)]
    public string? MethodName { get; set; }

    public TestOutcome Outcome { get; set; }

    /// <summary>Test execution duration in milliseconds.</summary>
    public double DurationMs { get; set; }

    [MaxLength(4000)]
    public string? ErrorMessage { get; set; }

    public string? StackTrace { get; set; }
}
