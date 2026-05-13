using Microsoft.Extensions.AI;

namespace SemanticAssertions.Assertions;

/// <summary>
/// Global configuration options for SemanticAssertions.
/// Set a default grader to avoid passing .GradedBy() on every assertion.
/// </summary>
public sealed class SemanticOptions
{
    /// <summary>
    /// Default IChatClient used for semantic grading when .GradedBy() is not specified.
    /// </summary>
    public IChatClient? DefaultGrader { get; set; }

    /// <summary>
    /// Default timeout for agent execution and grading operations.
    /// </summary>
    public TimeSpan? DefaultTimeout { get; set; }

    /// <summary>
    /// Default minimum coverage threshold (0.0 to 1.0).
    /// </summary>
    public double? DefaultMinimumCoverage { get; set; }

    /// <summary>
    /// Default minimum confidence threshold (0.0 to 1.0). Informational by default.
    /// </summary>
    public double? DefaultMinimumConfidence { get; set; }
}
