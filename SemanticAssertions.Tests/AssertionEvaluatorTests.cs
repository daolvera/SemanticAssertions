using Microsoft.Extensions.AI;
using SemanticAssertions.Exceptions;
using SemanticAssertions.Grading;
using SemanticAssertions.Models;

namespace SemanticAssertions.Tests;

public class AssertionEvaluatorTests
{
    private static AgentRunResult CreateRunResult(params ToolInvocation[] invocations) => new()
    {
        FinalResponse = "Test response",
        Messages = [new ChatMessage(ChatRole.Assistant, "Test response")],
        ToolInvocations = invocations
    };

    #region CheckToolUsage

    [Fact]
    public void CheckToolUsage_AllExpectedToolsCalled_ReturnsNoFailures()
    {
        var result = CreateRunResult(
            new ToolInvocation { ToolName = "GetWeather" },
            new ToolInvocation { ToolName = "GetNews" });

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: ["GetWeather", "GetNews"],
            expectedToolOrder: null,
            forbiddenTools: null,
            toolPredicates: null);

        Assert.Empty(failures);
    }

    [Fact]
    public void CheckToolUsage_MissingExpectedTool_ReturnsFailure()
    {
        var result = CreateRunResult(new ToolInvocation { ToolName = "GetWeather" });

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: ["GetWeather", "GetNews"],
            expectedToolOrder: null,
            forbiddenTools: null,
            toolPredicates: null);

        Assert.Single(failures);
        Assert.Contains("GetNews", failures[0].Description);
        Assert.Contains("not called", failures[0].Description);
    }

    [Fact]
    public void CheckToolUsage_ExpectedToolMatchesCaseInsensitive()
    {
        var result = CreateRunResult(new ToolInvocation { ToolName = "getweather" });

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: ["GetWeather"],
            expectedToolOrder: null,
            forbiddenTools: null,
            toolPredicates: null);

        Assert.Empty(failures);
    }

    [Fact]
    public void CheckToolUsage_CorrectOrder_ReturnsNoFailures()
    {
        var result = CreateRunResult(
            new ToolInvocation { ToolName = "IdentifyCity" },
            new ToolInvocation { ToolName = "GetWeather" },
            new ToolInvocation { ToolName = "FormatResponse" });

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: null,
            expectedToolOrder: ["IdentifyCity", "GetWeather"],
            forbiddenTools: null,
            toolPredicates: null);

        Assert.Empty(failures);
    }

    [Fact]
    public void CheckToolUsage_WrongOrder_ReturnsFailure()
    {
        var result = CreateRunResult(
            new ToolInvocation { ToolName = "GetWeather" },
            new ToolInvocation { ToolName = "IdentifyCity" });

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: null,
            expectedToolOrder: ["IdentifyCity", "GetWeather"],
            forbiddenTools: null,
            toolPredicates: null);

        Assert.Single(failures);
        Assert.Contains("not called in expected order", failures[0].Description);
    }

    [Fact]
    public void CheckToolUsage_SubsequenceOrderWithExtraTools_ReturnsNoFailures()
    {
        var result = CreateRunResult(
            new ToolInvocation { ToolName = "Setup" },
            new ToolInvocation { ToolName = "IdentifyCity" },
            new ToolInvocation { ToolName = "Intermediate" },
            new ToolInvocation { ToolName = "GetWeather" });

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: null,
            expectedToolOrder: ["IdentifyCity", "GetWeather"],
            forbiddenTools: null,
            toolPredicates: null);

        Assert.Empty(failures);
    }

    [Fact]
    public void CheckToolUsage_ForbiddenToolCalled_ReturnsFailure()
    {
        var result = CreateRunResult(
            new ToolInvocation { ToolName = "GetWeather" },
            new ToolInvocation { ToolName = "SearchWeb" });

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: null,
            expectedToolOrder: null,
            forbiddenTools: ["SearchWeb"],
            toolPredicates: null);

        Assert.Single(failures);
        Assert.Contains("SearchWeb", failures[0].Description);
        Assert.Contains("Forbidden", failures[0].Description);
    }

    [Fact]
    public void CheckToolUsage_ForbiddenToolNotCalled_ReturnsNoFailures()
    {
        var result = CreateRunResult(new ToolInvocation { ToolName = "GetWeather" });

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: null,
            expectedToolOrder: null,
            forbiddenTools: ["SearchWeb"],
            toolPredicates: null);

        Assert.Empty(failures);
    }

    [Fact]
    public void CheckToolUsage_PredicateSatisfied_ReturnsNoFailures()
    {
        var result = CreateRunResult(
            new ToolInvocation { ToolName = "GetWeather",
                Arguments = new Dictionary<string, object?> { ["city"] = "Seattle" }
            });

        Func<ToolInvocation, bool> predicate = inv =>
            inv.ToolName == "GetWeather" &&
            inv.Arguments?["city"]?.ToString() == "Seattle";

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: null,
            expectedToolOrder: null,
            forbiddenTools: null,
            toolPredicates: [predicate]);

        Assert.Empty(failures);
    }

    [Fact]
    public void CheckToolUsage_PredicateNotSatisfied_ReturnsFailure()
    {
        var result = CreateRunResult(
            new ToolInvocation { ToolName = "GetWeather",
                Arguments = new Dictionary<string, object?> { ["city"] = "Portland" }
            });

        Func<ToolInvocation, bool> predicate = inv =>
            inv.Arguments?["city"]?.ToString() == "Seattle";

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: null,
            expectedToolOrder: null,
            forbiddenTools: null,
            toolPredicates: [predicate]);

        Assert.Single(failures);
        Assert.Contains("predicate was not satisfied", failures[0].Description);
    }

    [Fact]
    public void CheckToolUsage_NoToolsCalled_MissingExpected_ReturnsFailures()
    {
        var result = CreateRunResult();

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: ["GetWeather", "GetNews"],
            expectedToolOrder: null,
            forbiddenTools: null,
            toolPredicates: null);

        Assert.Equal(2, failures.Count);
    }

    [Fact]
    public void CheckToolUsage_NullParameters_ReturnsNoFailures()
    {
        var result = CreateRunResult(new ToolInvocation { ToolName = "Anything" });

        var failures = AssertionEvaluator.CheckToolUsage(
            result,
            expectedTools: null,
            expectedToolOrder: null,
            forbiddenTools: null,
            toolPredicates: null);

        Assert.Empty(failures);
    }

    #endregion

    #region CheckGraderResult

    [Fact]
    public void CheckGraderResult_Passed_NoThresholds_ReturnsNoFailures()
    {
        var graderResult = new GraderResult
        {
            Passed = true,
            Coverage = 1.0,
            Confidence = 0.95,
            MetCriteria = ["State A", "State B"]
        };

        var failures = AssertionEvaluator.CheckGraderResult(graderResult, null, null);

        Assert.Empty(failures);
    }

    [Fact]
    public void CheckGraderResult_Failed_ReturnsUnmetStateFailures()
    {
        var graderResult = new GraderResult
        {
            Passed = false,
            Coverage = 0.5,
            Confidence = 0.9,
            Reasoning = "State B was not reached",
            MetCriteria = ["State A"],
            UnmetCriteria = ["State B"]
        };

        var failures = AssertionEvaluator.CheckGraderResult(graderResult, null, null);

        Assert.Single(failures);
        Assert.Contains("State B", failures[0].Description);
    }

    [Fact]
    public void CheckGraderResult_Failed_NoUnmetCriteria_ReturnsGenericFailure()
    {
        var graderResult = new GraderResult
        {
            Passed = false,
            Coverage = 0.0,
            Reasoning = "Something went wrong"
        };

        var failures = AssertionEvaluator.CheckGraderResult(graderResult, null, null);

        Assert.Single(failures);
        Assert.Contains("Semantic grading failed", failures[0].Description);
    }

    [Fact]
    public void CheckGraderResult_CoverageBelowThreshold_ReturnsFailure()
    {
        var graderResult = new GraderResult
        {
            Passed = true,
            Coverage = 0.6,
            Confidence = 0.9,
            MetCriteria = ["A", "B"],
            UnmetCriteria = ["C"]
        };

        var failures = AssertionEvaluator.CheckGraderResult(graderResult, minimumCoverage: 0.9, minimumConfidence: null);

        Assert.Single(failures);
        Assert.Contains("Coverage", failures[0].Description);
        Assert.Contains("below", failures[0].Description);
    }

    [Fact]
    public void CheckGraderResult_CoverageAboveThreshold_ReturnsNoFailures()
    {
        var graderResult = new GraderResult
        {
            Passed = true,
            Coverage = 1.0,
            Confidence = 0.9
        };

        var failures = AssertionEvaluator.CheckGraderResult(graderResult, minimumCoverage: 0.9, minimumConfidence: null);

        Assert.Empty(failures);
    }

    [Fact]
    public void CheckGraderResult_ConfidenceBelowThreshold_ReturnsFailure()
    {
        var graderResult = new GraderResult
        {
            Passed = true,
            Coverage = 1.0,
            Confidence = 0.5,
            Reasoning = "Uncertain about state B"
        };

        var failures = AssertionEvaluator.CheckGraderResult(graderResult, null, minimumConfidence: 0.8);

        Assert.Single(failures);
        Assert.Contains("Confidence", failures[0].Description);
    }

    [Fact]
    public void CheckGraderResult_NullResult_ReturnsFailure()
    {
        var failures = AssertionEvaluator.CheckGraderResult(null, null, null);

        Assert.Single(failures);
        Assert.Contains("did not return a result", failures[0].Description);
    }

    #endregion

    #region ThrowIfFailed

    [Fact]
    public void ThrowIfFailed_NoFailures_DoesNotThrow()
    {
        AssertionEvaluator.ThrowIfFailed([], null, null, null);
    }

    [Fact]
    public void ThrowIfFailed_ToolFailure_ThrowsToolNotCalledException()
    {
        var failures = new List<AssertionFailure>
        {
            new("Expected tool 'GetWeather' was not called")
        };

        var result = CreateRunResult();

        var ex = Assert.Throws<ToolNotCalledException>(() =>
            AssertionEvaluator.ThrowIfFailed(failures, null, result, null));

        Assert.Contains("GetWeather", ex.ExpectedTools);
    }

    [Fact]
    public void ThrowIfFailed_CoverageFailure_ThrowsCoverageThresholdException()
    {
        var graderResult = new GraderResult { Coverage = 0.5 };
        var failures = new List<AssertionFailure>
        {
            new("Coverage 50% is below minimum 90%")
        };

        var ex = Assert.Throws<CoverageThresholdException>(() =>
            AssertionEvaluator.ThrowIfFailed(failures, graderResult, null, 0.9));

        Assert.Equal(0.5, ex.ActualCoverage);
        Assert.Equal(0.9, ex.RequiredCoverage);
    }

    [Fact]
    public void ThrowIfFailed_StateFailure_ThrowsEssentialStateMissedException()
    {
        var graderResult = new GraderResult
        {
            UnmetCriteria = ["State B"]
        };
        var failures = new List<AssertionFailure>
        {
            new("Essential state not met: 'State B'")
        };

        var ex = Assert.Throws<EssentialStateMissedException>(() =>
            AssertionEvaluator.ThrowIfFailed(failures, graderResult, null, null));

        Assert.Contains("State B", ex.MissedStates);
    }

    [Fact]
    public void ThrowIfFailed_GenericFailure_ThrowsSemanticAssertionException()
    {
        var failures = new List<AssertionFailure>
        {
            new("Something unexpected happened")
        };

        var ex = Assert.Throws<SemanticAssertionException>(() =>
            AssertionEvaluator.ThrowIfFailed(failures, null, null, null));

        Assert.Single(ex.Failures);
    }

    [Fact]
    public void ThrowIfFailed_AggregatesMultipleFailures()
    {
        var failures = new List<AssertionFailure>
        {
            new("Error one"),
            new("Error two"),
            new("Error three")
        };

        var ex = Assert.Throws<SemanticAssertionException>(() =>
            AssertionEvaluator.ThrowIfFailed(failures, null, null, null));

        Assert.Equal(3, ex.Failures.Count);
    }

    #endregion
}
