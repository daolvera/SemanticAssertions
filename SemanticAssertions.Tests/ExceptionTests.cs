using SemanticAssertions.Exceptions;
using SemanticAssertions.Models;

namespace SemanticAssertions.Tests;

public class ExceptionTests
{
    #region ToolNotCalledException

    [Fact]
    public void ToolNotCalledException_FormatsMessage_WithExpectedAndActual()
    {
        var failures = new List<AssertionFailure>
        {
            new("Expected tool 'GetWeather' was not called")
        };

        var ex = new ToolNotCalledException(
            expectedTools: ["GetWeather", "GetNews"],
            actualTools: ["Echo"],
            failures: failures);

        Assert.Contains("GetWeather", ex.Message);
        Assert.Contains("GetNews", ex.Message);
        Assert.Contains("Echo", ex.Message);
        Assert.Equal(["GetWeather", "GetNews"], ex.ExpectedTools);
        Assert.Equal(["Echo"], ex.ActualTools);
    }

    [Fact]
    public void ToolNotCalledException_FormatsMessage_WhenNoToolsCalled()
    {
        var ex = new ToolNotCalledException(
            expectedTools: ["GetWeather"],
            actualTools: [],
            failures: [new("Expected tool 'GetWeather' was not called")]);

        Assert.Contains("(none)", ex.Message);
    }

    #endregion

    #region EssentialStateMissedException

    [Fact]
    public void EssentialStateMissedException_ListsMissedStates()
    {
        var graderResult = new GraderResult { Coverage = 0.33 };
        var ex = new EssentialStateMissedException(
            missedStates: ["Weather retrieved", "Temperature reported"],
            failures: [new("state missed")],
            graderResult: graderResult);

        Assert.Contains("Weather retrieved", ex.Message);
        Assert.Contains("Temperature reported", ex.Message);
        Assert.Contains("33%", ex.Message);
        Assert.Equal(2, ex.MissedStates.Count);
    }

    [Fact]
    public void EssentialStateMissedException_OmitsCoverage_WhenNoGraderResult()
    {
        var ex = new EssentialStateMissedException(
            missedStates: ["State A"],
            failures: [new("missed")],
            graderResult: null);

        Assert.DoesNotContain("Coverage", ex.Message);
    }

    #endregion

    #region CoverageThresholdException

    [Fact]
    public void CoverageThresholdException_ShowsActualAndRequired()
    {
        var graderResult = new GraderResult
        {
            Coverage = 0.66,
            UnmetCriteria = ["State C"]
        };
        var ex = new CoverageThresholdException(
            actualCoverage: 0.66,
            requiredCoverage: 0.9,
            failures: [new("coverage low")],
            graderResult: graderResult);

        Assert.Contains("66%", ex.Message);
        Assert.Contains("90%", ex.Message);
        Assert.Contains("State C", ex.Message);
        Assert.Equal(0.66, ex.ActualCoverage);
        Assert.Equal(0.9, ex.RequiredCoverage);
    }

    #endregion

    #region SemanticAssertionException (base)

    [Fact]
    public void SemanticAssertionException_ContainsAggregateFailures()
    {
        var failures = new List<AssertionFailure>
        {
            new("Error one", "Detail one"),
            new("Error two", "Detail two"),
            new("Error three")
        };

        var ex = new SemanticAssertionException("Multiple failures", failures);

        Assert.Equal(3, ex.Failures.Count);
        Assert.Equal("Error one", ex.Failures[0].Description);
        Assert.Equal("Detail one", ex.Failures[0].Detail);
        Assert.Null(ex.Failures[2].Detail);
    }

    [Fact]
    public void SemanticAssertionException_AttachesGraderResult()
    {
        var graderResult = new GraderResult
        {
            Passed = false,
            Coverage = 0.5,
            Reasoning = "Half the states were missed"
        };

        var ex = new SemanticAssertionException(
            "Failed", [new("failure")], graderResult: graderResult);

        Assert.NotNull(ex.GraderResult);
        Assert.Equal(0.5, ex.GraderResult!.Coverage);
    }

    [Fact]
    public void SemanticAssertionException_AttachesToolInvocations()
    {
        var invocations = new List<ToolInvocation>
        {
            new() { ToolName = "GetWeather" },
            new() { ToolName = "FormatResponse" }
        };

        var ex = new SemanticAssertionException(
            "Failed", [new("failure")], toolInvocations: invocations);

        Assert.NotNull(ex.ToolInvocations);
        Assert.Equal(2, ex.ToolInvocations!.Count);
    }

    #endregion
}
