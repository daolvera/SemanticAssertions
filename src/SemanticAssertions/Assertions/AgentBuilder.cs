using Microsoft.Extensions.AI;
using SemanticAssertions.Agents;
using SemanticAssertions.Models;

namespace SemanticAssertions.Assertions;

/// <summary>
/// Fluent builder for configuring and executing an agent query with tools.
/// </summary>
public sealed class AgentBuilder
{
    private readonly IChatClient _chatClient;
    private IList<AITool>? _tools;
    private string? _systemPrompt;

    internal AgentBuilder(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    /// <summary>
    /// Specify the tools available to the agent. Accepts any AITool including McpClientTool.
    /// </summary>
    public AgentBuilder WithTools(IList<AITool> tools)
    {
        _tools = tools;
        return this;
    }

    /// <summary>
    /// Optionally provide a system prompt for the agent.
    /// </summary>
    public AgentBuilder WithSystemPrompt(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        return this;
    }

    /// <summary>
    /// Execute the agent query and return the captured result.
    /// </summary>
    public async Task<AgentRunResult> QueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var agent = new ToolAgent(_chatClient);
        return await agent.QueryAsync(
            query,
            _tools ?? [],
            _systemPrompt,
            cancellationToken);
    }
}
