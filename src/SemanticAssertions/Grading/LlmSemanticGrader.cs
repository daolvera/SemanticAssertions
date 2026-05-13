using Microsoft.Extensions.AI;
using SemanticAssertions.Models;
using System.Text;
using System.Text.Json;

namespace SemanticAssertions.Grading;

/// <summary>
/// LLM-based semantic grader that evaluates agent responses against essential states
/// and expected behavior. Uses structured JSON output for reliable parsing.
/// </summary>
public sealed class LlmSemanticGrader : ISemanticGrader
{
    private readonly IChatClient _chatClient;

    public LlmSemanticGrader(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        _chatClient = chatClient;
    }

    public async Task<GraderResult?> GradeAsync(
        AgenticTestCase testCase,
        AgentRunResult runResult,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(testCase, runResult);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var options = new ChatOptions
        {
            Temperature = 0f
        };

        var response = await _chatClient.GetResponseAsync<GraderResult>(
            messages, options, true, cancellationToken);

        return response.TryGetResult(out var graderResult)
            ? graderResult : null;
    }

    internal static string BuildSystemPrompt() =>
        """
        You are an impartial grading agent. Your job is to evaluate whether an AI agent's
        response correctly achieves the expected behavior and essential states.

        RULES:
        1. Evaluate each essential state independently.
        2. An essential state is "met" if the agent's response demonstrates that the state
           was reached, regardless of HOW it was reached.
        3. Essential states must appear in the specified ORDER — if State B depends on State A,
           then evidence of A must precede evidence of B in the execution trace.
        4. Set "passed" to true ONLY if ALL essential states are met in the correct order.
        5. Calculate "coverage" as (number of met states) / (total number of states).
        6. Provide specific reasoning for each met/unmet criterion.
        7. Set "confidence" to reflect how certain you are in your assessment (0.0 to 1.0).

        Respond ONLY with valid JSON matching the GraderResult schema.
        """;

    internal static string BuildUserPrompt(AgenticTestCase testCase, AgentRunResult runResult)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Original Query");
        sb.AppendLine(testCase.Query);
        sb.AppendLine();

        if (runResult.ToolInvocations.Count > 0)
        {
            sb.AppendLine("## Tools Called");
            foreach (var invocation in runResult.ToolInvocations)
            {
                var args = invocation.Arguments is not null
                    ? JsonSerializer.Serialize(invocation.Arguments)
                    : "none";
                sb.AppendLine($"- {invocation.ToolName}({args}) [Succeeded: {invocation.Succeeded}]");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Agent Response");
        sb.AppendLine(runResult.FinalResponse);
        sb.AppendLine();

        if (testCase.ExpectedBehavior is not null)
        {
            sb.AppendLine("## Expected Behavior");
            sb.AppendLine(testCase.ExpectedBehavior);
            sb.AppendLine();
        }

        if (testCase.EssentialStates.Length > 0)
        {
            sb.AppendLine("## Essential States (must be reached in this order)");
            for (var i = 0; i < testCase.EssentialStates.Length; i++)
            {
                sb.AppendLine($"{i + 1}. {testCase.EssentialStates[i]}");
            }
        }

        return sb.ToString();
    }
}
