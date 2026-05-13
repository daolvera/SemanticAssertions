using Microsoft.Extensions.AI;
using SemanticAssertions.Grading;
using SemanticAssertions.Models;

namespace SemanticAssertions.Assertions;

/// <summary>
/// Fluent builder for asserting on an existing AgentRunResult.
/// Pure assertion — does not execute the agent.
/// </summary>
public sealed class ResponseAssertionBuilder
{
    private readonly AgentRunResult _runResult;
    private readonly SemanticOptions _options;

    private readonly List<string> _expectedTools = [];
    private readonly List<string> _expectedToolOrder = [];
    private readonly List<string> _forbiddenTools = [];
    private readonly List<Func<ToolInvocation, bool>> _toolPredicates = [];
    private readonly List<string> _essentialStates = [];
    private string? _expectedBehavior;
    private double? _minimumCoverage;
    private double? _minimumConfidence;
    private IChatClient? _graderClient;

    internal ResponseAssertionBuilder(AgentRunResult runResult, SemanticOptions options)
    {
        _runResult = runResult;
        _options = options;
    }

    /// <summary>
    /// Assert that a specific tool was called (order-independent).
    /// </summary>
    public ResponseAssertionBuilder UsedTool(string toolName)
    {
        _expectedTools.Add(toolName);
        return this;
    }

    /// <summary>
    /// Assert that a tool was called matching a custom predicate (e.g., checking arguments).
    /// </summary>
    public ResponseAssertionBuilder UsedTool(string toolName, Func<ToolInvocation, bool> predicate)
    {
        _expectedTools.Add(toolName);
        _toolPredicates.Add(predicate);
        return this;
    }

    /// <summary>
    /// Assert that multiple tools were called (order-independent).
    /// </summary>
    public ResponseAssertionBuilder UsedTools(params string[] toolNames)
    {
        _expectedTools.AddRange(toolNames);
        return this;
    }

    /// <summary>
    /// Assert that tools were called in a specific order (subsequence matching).
    /// </summary>
    public ResponseAssertionBuilder UsedToolsInOrder(params string[] toolNames)
    {
        _expectedToolOrder.AddRange(toolNames);
        return this;
    }

    /// <summary>
    /// Assert that a tool was NOT called.
    /// </summary>
    public ResponseAssertionBuilder DidNotUseTool(string toolName)
    {
        _forbiddenTools.Add(toolName);
        return this;
    }

    /// <summary>
    /// Describe the expected behavior for semantic grading.
    /// </summary>
    public ResponseAssertionBuilder MeetsExpectation(string expectedBehavior)
    {
        _expectedBehavior = expectedBehavior;
        return this;
    }

    /// <summary>
    /// Define essential states that must be reached in order.
    /// These are the dominator-inspired milestones that every correct execution must pass through.
    /// </summary>
    public ResponseAssertionBuilder ReachedEssentialStates(params string[] states)
    {
        _essentialStates.AddRange(states);
        return this;
    }

    /// <summary>
    /// Set the minimum coverage threshold (matched essential states / total).
    /// </summary>
    public ResponseAssertionBuilder WithMinimumCoverage(double coverage)
    {
        _minimumCoverage = coverage;
        return this;
    }

    /// <summary>
    /// Set the minimum confidence threshold. Informational — prefer coverage for pass/fail.
    /// </summary>
    public ResponseAssertionBuilder WithMinimumConfidence(double confidence)
    {
        _minimumConfidence = confidence;
        return this;
    }

    /// <summary>
    /// Specify the IChatClient to use for semantic grading.
    /// Overrides the global default set via Semantic.Configure().
    /// </summary>
    public ResponseAssertionBuilder GradedBy(IChatClient graderClient)
    {
        _graderClient = graderClient;
        return this;
    }

    /// <summary>
    /// Execute all assertions. Deterministic checks run first, then semantic grading (if needed).
    /// Throws specific exceptions on failure with aggregate diagnostics.
    /// </summary>
    public async Task<GraderResult?> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var allFailures = new List<Exceptions.AssertionFailure>();

        // Phase 1: Deterministic tool checks
        var toolFailures = AssertionEvaluator.CheckToolUsage(
            _runResult,
            _expectedTools.Count > 0 ? _expectedTools : null,
            _expectedToolOrder.Count > 0 ? _expectedToolOrder : null,
            _forbiddenTools.Count > 0 ? _forbiddenTools : null,
            _toolPredicates.Count > 0 ? _toolPredicates : null);
        allFailures.AddRange(toolFailures);

        // Phase 2: Semantic grading (if essential states or expected behavior specified)
        GraderResult? graderResult = null;
        var requiresSemanticGrading = _essentialStates.Count > 0 || _expectedBehavior is not null;

        if (requiresSemanticGrading)
        {
            var graderClient = _graderClient ?? _options.DefaultGrader;
            if (graderClient is null)
            {
                throw new InvalidOperationException(
                    "Semantic grading requires a grader. Call .GradedBy(chatClient) or " +
                    "configure Semantic.Configure(o => o.DefaultGrader = chatClient).");
            }

            var testCase = new AgenticTestCase
            {
                Query = _runResult.FinalResponse,
                ExpectedBehavior = _expectedBehavior,
                EssentialStates = [.. _essentialStates]
            };

            var grader = new LlmSemanticGrader(graderClient);
            graderResult = await grader.GradeAsync(testCase, _runResult, cancellationToken);

            var coverageThreshold = _minimumCoverage ?? _options.DefaultMinimumCoverage;
            var confidenceThreshold = _minimumConfidence ?? _options.DefaultMinimumConfidence;

            var graderFailures = AssertionEvaluator.CheckGraderResult(
                graderResult, coverageThreshold, confidenceThreshold);
            allFailures.AddRange(graderFailures);
        }

        // Phase 3: Throw if any failures
        AssertionEvaluator.ThrowIfFailed(
            allFailures, graderResult, _runResult, _minimumCoverage ?? _options.DefaultMinimumCoverage);

        return graderResult;
    }
}
