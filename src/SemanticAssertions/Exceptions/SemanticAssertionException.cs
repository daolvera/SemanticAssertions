using SemanticAssertions.Models;

namespace SemanticAssertions.Exceptions;

/// <summary>
/// Base exception for semantic assertion failures. Contains an aggregate list of
/// all failures and optional grading result for diagnostics.
/// </summary>
public class SemanticAssertionException : Exception
{
    public IReadOnlyList<AssertionFailure> Failures { get; }

    public GraderResult? GraderResult { get; }

    public IReadOnlyList<ToolInvocation>? ToolInvocations { get; }

    public SemanticAssertionException(
        string message,
        IReadOnlyList<AssertionFailure> failures,
        GraderResult? graderResult = null,
        IReadOnlyList<ToolInvocation>? toolInvocations = null)
        : base(message)
    {
        Failures = failures;
        GraderResult = graderResult;
        ToolInvocations = toolInvocations;
    }

    public SemanticAssertionException(
        string message,
        IReadOnlyList<AssertionFailure> failures,
        Exception innerException,
        GraderResult? graderResult = null,
        IReadOnlyList<ToolInvocation>? toolInvocations = null)
        : base(message, innerException)
    {
        Failures = failures;
        GraderResult = graderResult;
        ToolInvocations = toolInvocations;
    }
}
