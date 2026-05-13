using Microsoft.Extensions.AI;

namespace SemanticAssertions.Models;

/// <summary>
/// The complete result of an agent execution, including the final response,
/// full message transcript, and captured tool invocations.
/// </summary>
public sealed record AgentRunResult
{
    public required string FinalResponse { get; init; }

    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    public required IReadOnlyList<ToolInvocation> ToolInvocations { get; init; }
}
