using System.Text.Json;
using Microsoft.Extensions.AI;
using Moq;
using Moq.AutoMock;
using SemanticAssertions.Assertions;
using SemanticAssertions.Models;

namespace SemanticAssertions.Tests;

[Collection("SemanticState")]
public class PipelineTests : IDisposable
{
    public PipelineTests()
    {
        Semantic.ResetConfiguration();
    }

    public void Dispose()
    {
        Semantic.ResetConfiguration();
    }

    #region Semantic.Configure

    [Fact]
    public async Task Configure_DefaultGrader_UsedWhenGradedByNotSpecified()
    {
        var mocker = new AutoMocker();
        var mockClient = mocker.GetMock<IChatClient>();

        SetupMockGraderResponse(mockClient, new GraderResult
        {
            Passed = true,
            Coverage = 1.0,
            Confidence = 0.95,
            MetCriteria = ["State A"]
        });

        Semantic.Configure(o => o.DefaultGrader = mockClient.Object);

        var result = new AgentRunResult
        {
            FinalResponse = "Response",
            Messages = [new ChatMessage(ChatRole.Assistant, "Response")],
            ToolInvocations = []
        };

        // No .GradedBy() call — should use the default
        var graderResult = await Semantic
            .Assert(result)
            .MeetsExpectation("Some expectation")
            .ValidateAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(graderResult);
        Assert.True(graderResult!.Passed);
    }

    [Fact]
    public async Task Configure_DefaultMinimumCoverage_AppliedAutomatically()
    {
        var mocker = new AutoMocker();
        var mockClient = mocker.GetMock<IChatClient>();

        SetupMockGraderResponse(mockClient, new GraderResult
        {
            Passed = true,
            Coverage = 0.5,
            Confidence = 0.9,
            MetCriteria = ["A"],
            UnmetCriteria = ["B"]
        });

        Semantic.Configure(o =>
        {
            o.DefaultGrader = mockClient.Object;
            o.DefaultMinimumCoverage = 0.9;
        });

        var result = new AgentRunResult
        {
            FinalResponse = "Response",
            Messages = [new ChatMessage(ChatRole.Assistant, "Response")],
            ToolInvocations = []
        };

        // DefaultMinimumCoverage of 0.9 should cause failure since coverage is 0.5
        await Assert.ThrowsAsync<Exceptions.CoverageThresholdException>(() =>
            Semantic
                .Assert(result)
                .ReachedEssentialStates("A", "B")
                .ValidateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Configure_PerAssertionGrader_OverridesDefault()
    {
        var mocker = new AutoMocker();
        var defaultMock = mocker.GetMock<IChatClient>();
        var overrideMock = new Mock<IChatClient>();

        // Default returns failing result
        SetupMockGraderResponse(defaultMock, new GraderResult
        {
            Passed = false,
            Coverage = 0.0,
            UnmetCriteria = ["Everything"]
        });

        // Override returns passing result
        SetupMockGraderResponse(overrideMock, new GraderResult
        {
            Passed = true,
            Coverage = 1.0,
            Confidence = 1.0,
            MetCriteria = ["State A"]
        });

        Semantic.Configure(o => o.DefaultGrader = defaultMock.Object);

        var result = new AgentRunResult
        {
            FinalResponse = "Response",
            Messages = [new ChatMessage(ChatRole.Assistant, "Response")],
            ToolInvocations = []
        };

        // .GradedBy() should override the default
        var graderResult = await Semantic
            .Assert(result)
            .MeetsExpectation("Expectation")
            .GradedBy(overrideMock.Object)
            .ValidateAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(graderResult);
        Assert.True(graderResult!.Passed);

        // Verify override was called, default was not
        overrideMock.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        defaultMock.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region ResetConfiguration

    [Fact]
    public async Task ResetConfiguration_ClearsAllDefaults()
    {
        var mocker = new AutoMocker();
        var mockClient = mocker.GetMock<IChatClient>();

        Semantic.Configure(o =>
        {
            o.DefaultGrader = mockClient.Object;
            o.DefaultMinimumCoverage = 0.9;
        });

        Semantic.ResetConfiguration();

        var result = new AgentRunResult
        {
            FinalResponse = "Response",
            Messages = [new ChatMessage(ChatRole.Assistant, "Response")],
            ToolInvocations = []
        };

        // After reset, semantic grading without .GradedBy() should throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Semantic
                .Assert(result)
                .MeetsExpectation("Expectation")
                .ValidateAsync(TestContext.Current.CancellationToken));
    }

    #endregion

    #region ScenarioBuilder Validation

    [Fact]
    public async Task Scenario_NoQueryOrConversation_ThrowsInvalidOperationException()
    {
        var mocker = new AutoMocker();
        var mockClient = mocker.GetMock<IChatClient>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Semantic
                .Scenario(mockClient.Object)
                .ReachedEssentialStates("State A")
                .ValidateAsync(TestContext.Current.CancellationToken));
    }

    #endregion

    #region Combined Deterministic + Semantic

    [Fact]
    public async Task ValidateAsync_DeterministicAndSemantic_BothPass()
    {
        var mocker = new AutoMocker();
        var mockClient = mocker.GetMock<IChatClient>();

        SetupMockGraderResponse(mockClient, new GraderResult
        {
            Passed = true,
            Coverage = 1.0,
            Confidence = 0.95,
            MetCriteria = ["Weather retrieved"]
        });

        var result = new AgentRunResult
        {
            FinalResponse = "Seattle: 72°F",
            Messages = [new ChatMessage(ChatRole.Assistant, "Seattle: 72°F")],
            ToolInvocations = [new ToolInvocation { ToolName = "GetWeather" }]
        };

        var graderResult = await Semantic
            .Assert(result)
            .UsedTool("GetWeather")
            .ReachedEssentialStates("Weather retrieved")
            .GradedBy(mockClient.Object)
            .ValidateAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(graderResult);
        Assert.True(graderResult!.Passed);
    }

    [Fact]
    public async Task ValidateAsync_DeterministicFails_EvenIfSemanticWouldPass_Throws()
    {
        var mocker = new AutoMocker();
        var mockClient = mocker.GetMock<IChatClient>();

        // Grader would pass, but we never reach it because deterministic fails first
        SetupMockGraderResponse(mockClient, new GraderResult
        {
            Passed = true,
            Coverage = 1.0,
            MetCriteria = ["State A"]
        });

        var result = new AgentRunResult
        {
            FinalResponse = "Response",
            Messages = [new ChatMessage(ChatRole.Assistant, "Response")],
            ToolInvocations = [new ToolInvocation { ToolName = "Echo" }]
        };

        // Deterministic assertion will fail (GetWeather not called)
        // but semantic will also run and both failures are collected
        await Assert.ThrowsAsync<Exceptions.ToolNotCalledException>(() =>
            Semantic
                .Assert(result)
                .UsedTool("GetWeather")
                .ReachedEssentialStates("State A")
                .GradedBy(mockClient.Object)
                .ValidateAsync(TestContext.Current.CancellationToken));
    }

    #endregion

    #region Helpers

    private static void SetupMockGraderResponse(Mock<IChatClient> mockClient, GraderResult graderResult)
    {
        var responseJson = JsonSerializer.Serialize(graderResult);
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
