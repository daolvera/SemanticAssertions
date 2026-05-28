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
            $"Coverage {(int)Math.Round(actualCoverage * 100)}% is below minimum threshold {(int)Math.Round(requiredCoverage * 100)}%. " +
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
