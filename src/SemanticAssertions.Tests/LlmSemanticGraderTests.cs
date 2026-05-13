using Microsoft.Extensions.AI;
using SemanticAssertions.Grading;
using SemanticAssertions.Models;

namespace SemanticAssertions.Tests;

public class LlmSemanticGraderTests
{
    #region BuildSystemPrompt

    [Fact]
    public void BuildSystemPrompt_ContainsGradingRules()
    {
        var prompt = LlmSemanticGrader.BuildSystemPrompt();

        Assert.Contains("essential state", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coverage", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("confidence", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("passed", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER", prompt);
    }

    #endregion

    #region BuildUserPrompt

    [Fact]
    public void BuildUserPrompt_IncludesQuery()
    {
        var testCase = new AgenticTestCase { Query = "What is the weather?" };
        var runResult = CreateRunResult("The weather is sunny.");

        var prompt = LlmSemanticGrader.BuildUserPrompt(testCase, runResult);

        Assert.Contains("What is the weather?", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesAgentResponse()
    {
        var testCase = new AgenticTestCase { Query = "Test query" };
        var runResult = CreateRunResult("The answer is 42.");

        var prompt = LlmSemanticGrader.BuildUserPrompt(testCase, runResult);

        Assert.Contains("The answer is 42.", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesToolInvocations()
    {
        var testCase = new AgenticTestCase { Query = "Test" };
        var runResult = new AgentRunResult
        {
            FinalResponse = "Result",
            Messages = [new ChatMessage(ChatRole.Assistant, "Result")],
            ToolInvocations =
            [
                new ToolInvocation
                {
                    ToolName = "GetWeather",
                    Arguments = new Dictionary<string, object?> { ["city"] = "Seattle" }
                }
            ]
        };

        var prompt = LlmSemanticGrader.BuildUserPrompt(testCase, runResult);

        Assert.Contains("GetWeather", prompt);
        Assert.Contains("Seattle", prompt);
        Assert.Contains("Tools Called", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesExpectedBehavior()
    {
        var testCase = new AgenticTestCase
        {
            Query = "Test",
            ExpectedBehavior = "Should return temperature in Fahrenheit"
        };
        var runResult = CreateRunResult("72°F");

        var prompt = LlmSemanticGrader.BuildUserPrompt(testCase, runResult);

        Assert.Contains("Should return temperature in Fahrenheit", prompt);
        Assert.Contains("Expected Behavior", prompt);
    }

    [Fact]
    public void BuildUserPrompt_IncludesEssentialStatesNumbered()
    {
        var testCase = new AgenticTestCase
        {
            Query = "Test",
            EssentialStates = ["City identified", "Weather retrieved", "Temperature reported"]
        };
        var runResult = CreateRunResult("Seattle: 72°F");

        var prompt = LlmSemanticGrader.BuildUserPrompt(testCase, runResult);

        Assert.Contains("1. City identified", prompt);
        Assert.Contains("2. Weather retrieved", prompt);
        Assert.Contains("3. Temperature reported", prompt);
        Assert.Contains("Essential States", prompt);
    }

    [Fact]
    public void BuildUserPrompt_OmitsToolsSectionWhenNoInvocations()
    {
        var testCase = new AgenticTestCase { Query = "Test" };
        var runResult = CreateRunResult("Response");

        var prompt = LlmSemanticGrader.BuildUserPrompt(testCase, runResult);

        Assert.DoesNotContain("Tools Called", prompt);
    }

    [Fact]
    public void BuildUserPrompt_OmitsExpectedBehaviorWhenNull()
    {
        var testCase = new AgenticTestCase { Query = "Test", ExpectedBehavior = null };
        var runResult = CreateRunResult("Response");

        var prompt = LlmSemanticGrader.BuildUserPrompt(testCase, runResult);

        Assert.DoesNotContain("Expected Behavior", prompt);
    }

    [Fact]
    public void BuildUserPrompt_OmitsEssentialStatesWhenEmpty()
    {
        var testCase = new AgenticTestCase { Query = "Test", EssentialStates = [] };
        var runResult = CreateRunResult("Response");

        var prompt = LlmSemanticGrader.BuildUserPrompt(testCase, runResult);

        Assert.DoesNotContain("Essential States", prompt);
    }

    #endregion

    private static AgentRunResult CreateRunResult(string response) => new()
    {
        FinalResponse = response,
        Messages = [new ChatMessage(ChatRole.Assistant, response)],
        ToolInvocations = []
    };
}
