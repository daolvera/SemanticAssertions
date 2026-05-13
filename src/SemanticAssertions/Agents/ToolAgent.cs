using Microsoft.Extensions.AI;
using SemanticAssertions.Models;

namespace SemanticAssertions.Agents;

/// <summary>
/// Executes queries against an IChatClient with tool invocation enabled.
/// Captures the full execution trace including tool calls and arguments.
/// </summary>
public sealed class ToolAgent
{
    private readonly IChatClient _chatClient;

    public ToolAgent(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        _chatClient = chatClient;
    }

    public async Task<AgentRunResult> QueryAsync(
        string query,
        IList<AITool> tools,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var wrappedClient = new ChatClientBuilder(_chatClient)
            .UseFunctionInvocation()
            .Build();

        var messages = new List<ChatMessage>();

        if (systemPrompt is not null)
        {
            messages.Add(new ChatMessage(ChatRole.System, systemPrompt));
        }

        messages.Add(new ChatMessage(ChatRole.User, query));

        var options = new ChatOptions
        {
            Tools = tools
        };

        var response = await wrappedClient.GetResponseAsync(
            messages, options, cancellationToken);

        var invocations = response.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(fc => new ToolInvocation
            {
                ToolName = fc.Name,
                Arguments = fc.Arguments
            })
            .ToList();

        var finalText = response.Text ?? string.Empty;

        return new AgentRunResult
        {
            FinalResponse = finalText,
            Messages = [.. response.Messages],
            ToolInvocations = invocations
        };
    }
}
