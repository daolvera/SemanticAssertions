using SemanticAssertions.Models;

namespace SemanticAssertions.Grading;

/// <summary>
/// Defines the contract for semantic grading of agent responses.
/// Implementations evaluate whether an agent's response meets the expected behavior
/// and essential states defined in the test case.
/// </summary>
public interface ISemanticGrader
{
    Task<GraderResult?> GradeAsync(
        AgenticTestCase testCase,
        AgentRunResult runResult,
        CancellationToken cancellationToken = default);
}
