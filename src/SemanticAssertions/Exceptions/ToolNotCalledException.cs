using SemanticAssertions.Models;

namespace SemanticAssertions.Exceptions;

/// <summary>
/// Thrown when an expected tool was not invoked during agent execution.
/// </summary>
public sealed class ToolNotCalledException : SemanticAssertionException
{
    public IReadOnlyList<string> ExpectedTools { get; }
    public IReadOnlyList<string> ActualTools { get; }

    public ToolNotCalledException(
        IReadOnlyList<string> expectedTools,
        IReadOnlyList<string> actualTools,
        IReadOnlyList<AssertionFailure> failures,
        IReadOnlyList<ToolInvocation>? toolInvocations = null)
        : base(
            FormatMessage(expectedTools, actualTools),
            failures,
            graderResult: null,
            toolInvocations: toolInvocations)
    {
        ExpectedTools = expectedTools;
        ActualTools = actualTools;
    }

    private static string FormatMessage(
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual)
    {
        var expectedList = string.Join(", ", expected);
        var actualList = actual.Count > 0 ? string.Join(", ", actual) : "(none)";
        return $"Expected tools [{expectedList}] were not all called. Actual tools called: [{actualList}]";
    }
}
