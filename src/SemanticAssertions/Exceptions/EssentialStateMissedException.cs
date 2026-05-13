using SemanticAssertions.Models;

namespace SemanticAssertions.Exceptions;

/// <summary>
/// Thrown when one or more essential states were not reached during agent execution.
/// </summary>
public sealed class EssentialStateMissedException : SemanticAssertionException
{
    public IReadOnlyList<string> MissedStates { get; }

    public EssentialStateMissedException(
        IReadOnlyList<string> missedStates,
        IReadOnlyList<AssertionFailure> failures,
        GraderResult? graderResult = null,
        IReadOnlyList<ToolInvocation>? toolInvocations = null)
        : base(
            FormatMessage(missedStates, graderResult),
            failures,
            graderResult: graderResult,
            toolInvocations: toolInvocations)
    {
        MissedStates = missedStates;
    }

    private static string FormatMessage(
        IReadOnlyList<string> missedStates,
        GraderResult? graderResult)
    {
        var stateList = string.Join(", ", missedStates.Select(s => $"'{s}'"));
        var coverage = graderResult is not null
            ? $" Coverage: {graderResult.Coverage:P0}"
            : string.Empty;
        return $"Essential states not reached: [{stateList}].{coverage}";
    }
}
