namespace SemanticAssertions.Models;

/// <summary>
/// Defines a test case for agentic behavior validation.
/// Captures the query, expected tool usage, behavior expectations, and essential states
/// that must be reached for the test to pass.
/// </summary>
public sealed record AgenticTestCase
{
    public required string Query { get; init; }

    public string[]? ExpectedToolNames { get; init; }

    public string? ExpectedBehavior { get; init; }

    /// <summary>
    /// Essential states that must be reached in order during execution.
    /// Inspired by dominator analysis — these represent milestones that every
    /// correct execution must pass through, regardless of the path taken.
    /// </summary>
    public string[] EssentialStates { get; init; } = [];
}
