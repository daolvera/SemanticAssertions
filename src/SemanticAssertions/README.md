# SemanticAssertions

Fluent semantic assertions for testing agentic behavior and non-deterministic code.

## Theoretical Basis

This library is built on the concepts from [Validating agentic behavior when "correct" isn't deterministic](https://github.blog/ai-and-ml/generative-ai/validating-agentic-behavior-when-correct-isnt-deterministic/) — a GitHub blog post exploring how **dominator analysis** from compiler theory can validate autonomous agent behavior.

### Key Insight

Traditional tests assume a single correct execution path. Agentic systems can take many valid paths to the same result. SemanticAssertions shifts validation from "did this exact sequence happen?" to **"were all essential milestones reached?"**

- **Essential states**: Milestones that must occur for success — like dominator nodes in a control-flow graph
- **Coverage metric**: `matched_essential_states / total_essential_states`
- **Dual validation**: Deterministic tool checks + LLM-based semantic grading

## Installation

```bash
dotnet add package SemanticAssertions
```

## Dependencies

- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) — `IChatClient` abstraction
- [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) — MCP tool support

## Usage

### Configure a Grader

SemanticAssertions uses an `IChatClient` as a grading agent to evaluate responses semantically. Configure it globally or per-assertion:

```csharp
using SemanticAssertions.Assertions;

// Global default (recommended)
Semantic.Configure(options =>
{
    options.DefaultGrader = graderChatClient;
    options.DefaultMinimumCoverage = 0.9;
});

// Or per-assertion with .GradedBy(...)
```

> **Tip**: Use a different model for grading than the agent under test to avoid the "self-verification gap" described in the blog.

### Pattern 1: MCP Tool Usage Testing

Test that an agent correctly selects and uses MCP tools. This tests the **agent's behavior**, not the tools themselves.

```csharp
// Step 1: Execute the agent with tools
var response = await Semantic
    .Agent(chatClient)
    .WithTools(tools) // IList<AITool> — supports McpClientTool from MCP SDK
    .QueryAsync("What's the weather in Seattle?");

// Step 2: Assert tool usage and semantic correctness
await Semantic
    .Assert(response)
    .UsedTool("GetWeather")
    .DidNotUseTool("SearchWeb")
    .MeetsExpectation("Returns current weather data for Seattle")
    .ReachedEssentialStates(
        "Location is identified as Seattle",
        "Weather data is retrieved",
        "Temperature is reported"
    )
    .WithMinimumCoverage(0.9)
    .GradedBy(graderClient) // optional if global default is set
    .ValidateAsync();
```

### Pattern 2: Step-Based Chat Testing

Test `IChatClient` behavior with essential state validation — inspired by dominator graph theory.

```csharp
await Semantic
    .Scenario(chatClient)
    .ForQuery("Analyze this code and suggest improvements")
    .ReachedEssentialStates(
        "Code structure is analyzed",
        "Issues are identified",
        "Improvements are suggested with examples"
    )
    .WithMinimumCoverage(0.9)
    .ValidateAsync();
```

### Pattern 3: Multi-Turn Conversation Testing

Test that multi-turn conversations reach essential milestones across turns.

```csharp
await Semantic
    .Scenario(chatClient)
    .ForConversation(conversation => conversation
        .UserSays("What files are in my project?")
        .ThenUserSays("Refactor the main service")
    )
    .ReachedEssentialStates(
        "Project files are listed",
        "Main service code is read",
        "Refactored code is provided"
    )
    .WithMinimumCoverage(0.9)
    .ValidateAsync();
```

### Rich Tool Assertions

Beyond simple tool name checking, you can assert on tool ordering, arguments, and negative cases:

```csharp
await Semantic
    .Assert(response)
    .UsedTool("IdentifyCity")
    .UsedTool("GetWeather", invocation =>
        invocation.Arguments?["city"]?.ToString() == "Seattle")
    .UsedToolsInOrder("IdentifyCity", "GetWeather")
    .DidNotUseTool("FallbackSearch")
    .ValidateAsync();
```

## API Reference

### Entry Points

| Method | Description |
|--------|-------------|
| `Semantic.Agent(IChatClient)` | Create an agent builder for execution |
| `Semantic.Assert(AgentRunResult)` | Pure assertion on an existing result |
| `Semantic.Scenario(IChatClient)` | Execute + assert in a single chain |
| `Semantic.Configure(Action<SemanticOptions>)` | Set global defaults |

### ResponseAssertionBuilder

| Method | Description |
|--------|-------------|
| `.UsedTool(name)` | Assert tool was called |
| `.UsedTool(name, predicate)` | Assert tool was called matching predicate |
| `.UsedTools(names)` | Assert multiple tools were called |
| `.UsedToolsInOrder(names)` | Assert tools called in order |
| `.DidNotUseTool(name)` | Assert tool was NOT called |
| `.MeetsExpectation(description)` | Describe expected behavior for grading |
| `.ReachedEssentialStates(states)` | Define ordered essential milestones |
| `.WithMinimumCoverage(double)` | Set coverage threshold |
| `.WithMinimumConfidence(double)` | Set confidence threshold |
| `.GradedBy(IChatClient)` | Set grader for this assertion |
| `.ValidateAsync()` | Execute assertions, returns `GraderResult?` |

### Models

- **`AgentRunResult`** — Complete execution result (response, messages, tool invocations)
- **`GraderResult`** — Grading result (passed, confidence, coverage, reasoning, met/unmet criteria)
- **`ToolInvocation`** — Captured tool call (name, arguments, success)

### Exceptions

All exceptions extend `SemanticAssertionException` and contain:
- `Failures` — aggregate list of all assertion failures
- `GraderResult` — the semantic grading result (if applicable)
- `ToolInvocations` — the captured tool calls

| Exception | When |
|-----------|------|
| `ToolNotCalledException` | Expected tool was not invoked |
| `EssentialStateMissedException` | Essential state not reached |
| `CoverageThresholdException` | Coverage below minimum threshold |

## Architecture

```
Semantic (entry point)
├── Agent(IChatClient) → AgentBuilder → AgentRunResult
├── Assert(AgentRunResult) → ResponseAssertionBuilder → GraderResult
└── Scenario(IChatClient) → ScenarioBuilder → GraderResult

Grading Pipeline:
┌─────────────┐     ┌──────────────┐     ┌─────────────────┐
│ Deterministic│────▶│ Semantic     │────▶│ GraderResult    │
│ Tool Checks  │     │ LLM Grading  │     │ + Exceptions    │
└─────────────┘     └──────────────┘     └─────────────────┘
```

## License

MIT
