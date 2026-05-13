using Microsoft.Extensions.AI;
using SemanticAssertions.Agents;
using SemanticAssertions.Grading;
using SemanticAssertions.Models;

namespace SemanticAssertions.Assertions;

/// <summary>
/// Fluent builder that combines agent execution and assertion in a single chain.
/// Use Semantic.Scenario() for execute+grade workflows.
/// Note: ValidateAsync() will execute the chat — this is not a pure assertion.
/// </summary>
public sealed class ScenarioBuilder
{
    private readonly IChatClient _chatClient;
    private readonly SemanticOptions _options;

    private string? _query;
    private IReadOnlyList<string>? _conversationMessages;
    private IList<AITool>? _tools;
    private string? _systemPrompt;
    private readonly List<string> _essentialStates = [];
    private string? _expectedBehavior;
    private readonly List<string> _expectedTools = [];
    private readonly List<string> _forbiddenTools = [];
    private double? _minimumCoverage;
    private double? _minimumConfidence;
    private IChatClient? _graderClient;

    internal ScenarioBuilder(IChatClient chatClient, SemanticOptions options)
    {
        _chatClient = chatClient;
        _options = options;
    }

    /// <summary>
    /// Set the query for a single-turn scenario.
    /// </summary>
    public ScenarioBuilder ForQuery(string query)
    {
        _query = query;
        return this;
    }

    /// <summary>
    /// Set up a multi-turn conversation scenario.
    /// </summary>
    public ScenarioBuilder ForConversation(Func<ConversationBuilder, ConversationContinuationBuilder> configure)
    {
        var builder = new ConversationBuilder();
        var continuation = configure(builder);
        _conversationMessages = continuation.Build();
        return this;
    }

    /// <summary>
    /// Specify tools available to the agent.
    /// </summary>
    public ScenarioBuilder WithTools(IList<AITool> tools)
    {
        _tools = tools;
        return this;
    }

    /// <summary>
    /// Optionally provide a system prompt for the agent.
    /// </summary>
    public ScenarioBuilder WithSystemPrompt(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        return this;
    }

    /// <summary>
    /// Assert that a specific tool was called.
    /// </summary>
    public ScenarioBuilder UsedTool(string toolName)
    {
        _expectedTools.Add(toolName);
        return this;
    }

    /// <summary>
    /// Assert that a tool was NOT called.
    /// </summary>
    public ScenarioBuilder DidNotUseTool(string toolName)
    {
        _forbiddenTools.Add(toolName);
        return this;
    }

    /// <summary>
    /// Describe the expected behavior for semantic grading.
    /// </summary>
    public ScenarioBuilder MeetsExpectation(string expectedBehavior)
    {
        _expectedBehavior = expectedBehavior;
        return this;
    }

    /// <summary>
    /// Define essential states that must be reached in order.
    /// </summary>
    public ScenarioBuilder ReachedEssentialStates(params string[] states)
    {
        _essentialStates.AddRange(states);
        return this;
    }

    /// <summary>
    /// Set the minimum coverage threshold.
    /// </summary>
    public ScenarioBuilder WithMinimumCoverage(double coverage)
    {
        _minimumCoverage = coverage;
        return this;
    }

    /// <summary>
    /// Set the minimum confidence threshold.
    /// </summary>
    public ScenarioBuilder WithMinimumConfidence(double confidence)
    {
        _minimumConfidence = confidence;
        return this;
    }

    /// <summary>
    /// Specify the IChatClient to use for semantic grading.
    /// </summary>
    public ScenarioBuilder GradedBy(IChatClient graderClient)
    {
        _graderClient = graderClient;
        return this;
    }

    /// <summary>
    /// Execute the agent, then run all assertions.
    /// This method WILL execute the chat — it is not a pure assertion.
    /// </summary>
    public async Task<GraderResult?> ValidateAsync(CancellationToken cancellationToken = default)
    {
        // Validate configuration
        if (_query is null && _conversationMessages is null)
        {
            throw new InvalidOperationException(
                "No query or conversation configured. Call .ForQuery() or .ForConversation().");
        }

        // Execute the agent
        var runResult = _conversationMessages is not null
            ? await ExecuteConversationAsync(cancellationToken)
            : await ExecuteSingleQueryAsync(cancellationToken);

        // Delegate to ResponseAssertionBuilder for assertion logic
        var assertionBuilder = new ResponseAssertionBuilder(runResult, _options);

        foreach (var tool in _expectedTools)
            assertionBuilder.UsedTool(tool);

        foreach (var tool in _forbiddenTools)
            assertionBuilder.DidNotUseTool(tool);

        if (_expectedBehavior is not null)
            assertionBuilder.MeetsExpectation(_expectedBehavior);

        if (_essentialStates.Count > 0)
            assertionBuilder.ReachedEssentialStates([.. _essentialStates]);

        if (_minimumCoverage.HasValue)
            assertionBuilder.WithMinimumCoverage(_minimumCoverage.Value);

        if (_minimumConfidence.HasValue)
            assertionBuilder.WithMinimumConfidence(_minimumConfidence.Value);

        if (_graderClient is not null)
            assertionBuilder.GradedBy(_graderClient);

        return await assertionBuilder.ValidateAsync(cancellationToken);
    }

    private async Task<AgentRunResult> ExecuteSingleQueryAsync(CancellationToken cancellationToken)
    {
        var agent = new ToolAgent(_chatClient);
        return await agent.QueryAsync(_query!, _tools ?? [], _systemPrompt, cancellationToken);
    }

    private async Task<AgentRunResult> ExecuteConversationAsync(CancellationToken cancellationToken)
    {
        var wrappedClient = new ChatClientBuilder(_chatClient)
            .UseFunctionInvocation()
            .Build();

        var messages = new List<ChatMessage>();
        var allInvocations = new List<ToolInvocation>();
        var allMessages = new List<ChatMessage>();

        if (_systemPrompt is not null)
        {
            messages.Add(new ChatMessage(ChatRole.System, _systemPrompt));
        }

        var options = _tools is not null
            ? new ChatOptions { Tools = _tools }
            : null;

        ChatResponse? lastResponse = null;

        foreach (var userMessage in _conversationMessages!)
        {
            messages.Add(new ChatMessage(ChatRole.User, userMessage));

            lastResponse = await wrappedClient.GetResponseAsync(
                messages, options, cancellationToken);

            // Capture tool invocations from this turn
            var turnInvocations = lastResponse.Messages
                .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                .Select(fc => new ToolInvocation
                {
                    ToolName = fc.Name,
                    Arguments = fc.Arguments
                });
            allInvocations.AddRange(turnInvocations);

            // Add response messages to conversation history
            messages.AddRange(lastResponse.Messages);
            allMessages.AddRange(lastResponse.Messages);
        }

        return new AgentRunResult
        {
            FinalResponse = lastResponse?.Text ?? string.Empty,
            Messages = allMessages,
            ToolInvocations = allInvocations
        };
    }
}
