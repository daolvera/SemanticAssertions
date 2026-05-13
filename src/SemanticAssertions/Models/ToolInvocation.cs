namespace SemanticAssertions.Models;

/// <summary>
/// Represents a captured tool invocation from an agent execution.
/// </summary>
public sealed record ToolInvocation
{
    public required string ToolName { get; init; }

    public IDictionary<string, object?>? Arguments { get; init; }

    public bool Succeeded { get; init; } = true;
}
