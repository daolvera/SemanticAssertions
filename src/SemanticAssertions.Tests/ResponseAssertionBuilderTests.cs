using System.Text.Json;
using Microsoft.Extensions.AI;
using Moq;
using Moq.AutoMock;
using SemanticAssertions.Assertions;
using SemanticAssertions.Exceptions;
using SemanticAssertions.Models;

namespace SemanticAssertions.Tests;

[Collection("SemanticState")]
public class ResponseAssertionBuilderTests : IDisposable
{
    public ResponseAssertionBuilderTests()
    {
        Semantic.ResetConfiguration();
    }

    public void Dispose()
    {
        Semantic.ResetConfiguration();
    }

    private static AgentRunResult CreateRunResult(
        string response = "Test response",
        params ToolInvocation[] invocations) => new()
    {
        FinalResponse = response,
        Messages = [new ChatMessage(ChatRole.Assistant, response)],
        ToolInvocations = invocations
    };

    #region Deterministic-Only Assertions (no grader needed)

    [Fact]
    public async Task ValidateAsync_DeterministicOnly_AllToolsPresent_Passes()
    {
        var result = CreateRunResult("response",
            new ToolInvocation { ToolName = "GetWeather" },
            new ToolInvocation { ToolName = "FormatResponse" });

        var graderResult = await Semantic
            .Assert(result)
            .UsedTool("GetWeather")
            .UsedTool("FormatResponse")
            .ValidateAsync(TestContext.Current.CancellationToken);

        Assert.Null(graderResult);
    }

    [Fact]
    public async Task ValidateAsync_DeterministicOnly_MissingTool_ThrowsToolNotCalledException()
    {
        var result = CreateRunResult("response", new ToolInvocation { ToolName = "Echo" });

        await Assert.ThrowsAsync<ToolNotCalledException>(() =>
            Semantic
                .Assert(result)
                .UsedTool("GetWeather")
                .ValidateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidateAsync_UsedToolsInOrder_CorrectOrder_Passes()
    {
        var result = CreateRunResult("response",
            new ToolInvocation { ToolName = "A" },
            new ToolInvocation { ToolName = "B" },
            new ToolInvocation { ToolName = "C" });

        var graderResult = await Semantic
            .Assert(result)
            .UsedToolsInOrder("A", "C")
            .ValidateAsync(TestContext.Current.CancellationToken);

        Assert.Null(graderResult);
    }

    [Fact]
    public async Task ValidateAsync_DidNotUseTool_ToolWasCalled_Throws()
    {
        var result = CreateRunResult("response",
            new ToolInvocation { ToolName = "GetWeather" },
            new ToolInvocation { ToolName = "SearchWeb" });

        var ex = await Assert.ThrowsAsync<SemanticAssertionException>(() =>
            Semantic
                .Assert(result)
                .DidNotUseTool("SearchWeb")
                .ValidateAsync(TestContext.Current.CancellationToken));

        Assert.Contains(ex.Failures, f => f.Description.Contains("SearchWeb"));
    }

    [Fact]
    public async Task ValidateAsync_UsedToolWithPredicate_PredicateSatisfied_Passes()
    {
        var result = CreateRunResult("response",
            new ToolInvocation { ToolName = "GetWeather",
                Arguments = new Dictionary<string, object?> { ["city"] = "Seattle" }
            });

        var graderResult = await Semantic
            .Assert(result)
            .UsedTool("GetWeather", inv => inv.Arguments?["city"]?.ToString() == "Seattle")
            .ValidateAsync(TestContext.Current.CancellationToken);

        Assert.Null(graderResult);
    }

    [Fact]
    public async Task ValidateAsync_NoAssertionsConfigured_Passes()
    {
        var result = CreateRunResult("response");

        var graderResult = await Semantic
            .Assert(result)
            .ValidateAsync(TestContext.Current.CancellationToken);

        Assert.Null(graderResult);
    }

    #endregion

    #region Semantic Assertions (require grader)

    [Fact]
    public async Task ValidateAsync_SemanticWithoutGrader_ThrowsInvalidOperationException()
    {
        var result = CreateRunResult("response");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Semantic
                .Assert(result)
                .MeetsExpectation("Should do something")
                .ValidateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidateAsync_EssentialStatesWithoutGrader_ThrowsInvalidOperationException()
    {
        var result = CreateRunResult("response");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Semantic
                .Assert(result)
                .ReachedEssentialStates("State A", "State B")
                .ValidateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ValidateAsync_SemanticWithGrader_PassingResult_Passes()
    {
        var mocker = new AutoMocker();
        var mockClient = mocker.GetMock<IChatClient>();

        var graderJson = JsonSerializer.Serialize(new GraderResult
        {
            Passed = true,
            Coverage = 1.0,
            Confidence = 0.95,
            Reasoning = "All states met",
            MetCriteria = ["State A", "State B"],
            UnmetCriteria = []
        });

        SetupMockGraderResponse(mockClient, graderJson);

        var result = CreateRunResult("The weather in Seattle is 72°F");

        var graderResult = await Semantic
            .Assert(result)
            .MeetsExpectation("Returns weather data")
            .ReachedEssentialStates("State A", "State B")
            .GradedBy(mockClient.Object)
            .ValidateAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(graderResult);
        Assert.True(graderResult!.Passed);
    }

    [Fact]
    public async Task ValidateAsync_SemanticWithGrader_FailingResult_Throws()
    {
        var mocker = new AutoMocker();
        var mockClient = mocker.GetMock<IChatClient>();

        var graderJson = JsonSerializer.Serialize(new GraderResult
        {
            Passed = false,
            Coverage = 0.5,
            Confidence = 0.9,
            Reasoning = "State B was not reached",
            MetCriteria = ["State A"],
            UnmetCriteria = ["State B"]
        });

        SetupMockGraderResponse(mockClient, graderJson);

        var result = CreateRunResult("Partial response");

        var ex = await Assert.ThrowsAsync<EssentialStateMissedException>(() =>
            Semantic
                .Assert(result)
                .ReachedEssentialStates("State A", "State B")
                .GradedBy(mockClient.Object)
                .ValidateAsync(TestContext.Current.CancellationToken));

        Assert.Contains("State B", ex.MissedStates);
    }

    [Fact]
    public async Task ValidateAsync_CoverageBelowThreshold_ThrowsCoverageException()
    {
        var mocker = new AutoMocker();
        var mockClient = mocker.GetMock<IChatClient>();

        var graderJson = JsonSerializer.Serialize(new GraderResult
        {
            Passed = true,
            Coverage = 0.66,
            Confidence = 0.9,
            MetCriteria = ["State A", "State B"],
            UnmetCriteria = ["State C"]
        });

        SetupMockGraderResponse(mockClient, graderJson);

        var result = CreateRunResult("response");

        var ex = await Assert.ThrowsAsync<CoverageThresholdException>(() =>
            Semantic
                .Assert(result)
                .ReachedEssentialStates("State A", "State B", "State C")
                .WithMinimumCoverage(0.9)
                .GradedBy(mockClient.Object)
                .ValidateAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0.66, ex.ActualCoverage);
    }

    #endregion

    #region Helpers

    private static void SetupMockGraderResponse(Mock<IChatClient> mockClient, string responseJson)
    {
        var responseMessage = new ChatMessage(ChatRole.Assistant, responseJson);
        var chatResponse = new ChatResponse([responseMessage]);

        mockClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);
    }

    #endregion
}
