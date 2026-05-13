using SemanticAssertions.Models;

namespace SemanticAssertions.Exceptions;

/// <summary>
/// Thrown when the essential state coverage falls below the required minimum threshold.
/// </summary>
public sealed class CoverageThresholdException : SemanticAssertionException
{
    public double ActualCoverage { get; }
    public double RequiredCoverage { get; }

    public CoverageThresholdException(
        double actualCoverage,
        double requiredCoverage,
        IReadOnlyList<AssertionFailure> failures,
        GraderResult? graderResult = null,
        IReadOnlyList<ToolInvocation>? toolInvocations = null)
        : base(
            $"Coverage {actualCoverage:P0} is below minimum threshold {requiredCoverage:P0}. " +
            FormatUnmet(graderResult),
            failures,
            graderResult: graderResult,
            toolInvocations: toolInvocations)
    {
        ActualCoverage = actualCoverage;
        RequiredCoverage = requiredCoverage;
    }

    private static string FormatUnmet(GraderResult? graderResult)
    {
        if (graderResult is null || graderResult.UnmetCriteria.Length == 0)
            return string.Empty;

        return $"Unmet: [{string.Join(", ", graderResult.UnmetCriteria.Select(s => $"'{s}'"))}]";
    }
}
