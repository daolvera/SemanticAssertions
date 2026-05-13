namespace SemanticAssertions.Models;

/// <summary>
/// The result of semantic grading, including pass/fail status, coverage metrics,
/// and detailed reasoning for explainable validation.
/// </summary>
public sealed record GraderResult
{
    public bool Passed { get; init; }

    /// <summary>
    /// The grader's self-reported confidence in its assessment (0.0 to 1.0).
    /// Informational only — prefer coverage-based pass/fail over confidence thresholds.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Proportion of essential states that were matched (0.0 to 1.0).
    /// Calculated as matched_states / total_states.
    /// </summary>
    public double Coverage { get; init; }

    public string Reasoning { get; init; } = string.Empty;

    public string[] MetCriteria { get; init; } = [];

    public string[] UnmetCriteria { get; init; } = [];
}
