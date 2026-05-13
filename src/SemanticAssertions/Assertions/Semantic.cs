using Microsoft.Extensions.AI;
using SemanticAssertions.Models;

namespace SemanticAssertions.Assertions;

/// <summary>
/// Static entry point for the SemanticAssertions fluent API.
/// </summary>
public static class Semantic
{
    private static readonly SemanticOptions Options = new();

    /// <summary>
    /// Configure global defaults for all assertions.
    /// </summary>
    public static void Configure(Action<SemanticOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(Options);
    }

    /// <summary>
    /// Create an agent builder that executes queries with tools via IChatClient.
    /// Use this to run the agent and capture the result for later assertion.
    /// </summary>
    public static AgentBuilder Agent(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        return new AgentBuilder(chatClient);
    }

    /// <summary>
    /// Assert on an existing agent run result. Pure assertion — no execution.
    /// </summary>
    public static ResponseAssertionBuilder Assert(AgentRunResult response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new ResponseAssertionBuilder(response, Options);
    }

    /// <summary>
    /// Create a scenario that executes the agent and asserts on the result.
    /// Use when you want to combine execution and assertion in a single fluent chain.
    /// Note: ValidateAsync() will execute the chat — this is not a pure assertion.
    /// </summary>
    public static ScenarioBuilder Scenario(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        return new ScenarioBuilder(chatClient, Options);
    }

    /// <summary>
    /// Resets global configuration. Useful in test teardown.
    /// </summary>
    public static void ResetConfiguration()
    {
        Options.DefaultGrader = null;
        Options.DefaultTimeout = null;
        Options.DefaultMinimumCoverage = null;
        Options.DefaultMinimumConfidence = null;
    }
}
