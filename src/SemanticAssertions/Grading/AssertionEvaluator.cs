using SemanticAssertions.Exceptions;
using SemanticAssertions.Models;

namespace SemanticAssertions.Grading;

/// <summary>
/// Internal evaluator that orchestrates deterministic checks and semantic grading,
/// then produces assertion failures. Separates evaluation logic from the grader and builders.
/// </summary>
internal sealed class AssertionEvaluator
{
    internal static List<AssertionFailure> CheckToolUsage(
        AgentRunResult runResult,
        IReadOnlyList<string>? expectedTools,
        IReadOnlyList<string>? expectedToolOrder,
        IReadOnlyList<string>? forbiddenTools,
        IReadOnlyList<Func<ToolInvocation, bool>>? toolPredicates)
    {
        var failures = new List<AssertionFailure>();
        var actualToolNames = runResult.ToolInvocations
            .Select(i => i.ToolName)
            .ToList();
        var actualToolSet = new HashSet<string>(actualToolNames, StringComparer.OrdinalIgnoreCase);

        // Check expected tools were called
        if (expectedTools is { Count: > 0 })
        {
            foreach (var expected in expectedTools)
            {
                if (!actualToolSet.Contains(expected))
                {
                    failures.Add(new AssertionFailure(
                        $"Expected tool '{expected}' was not called",
                        $"Actual tools: [{string.Join(", ", actualToolNames)}]"));
                }
            }
        }

        // Check tool ordering
        if (expectedToolOrder is { Count: > 0 })
        {
            var orderIndex = 0;
            foreach (var actual in actualToolNames)
            {
                if (orderIndex < expectedToolOrder.Count &&
                    string.Equals(actual, expectedToolOrder[orderIndex], StringComparison.OrdinalIgnoreCase))
                {
                    orderIndex++;
                }
            }

            if (orderIndex < expectedToolOrder.Count)
            {
                failures.Add(new AssertionFailure(
                    $"Tools were not called in expected order",
                    $"Expected order: [{string.Join(" → ", expectedToolOrder)}], " +
                    $"Actual order: [{string.Join(" → ", actualToolNames)}]"));
            }
        }

        // Check forbidden tools
        if (forbiddenTools is { Count: > 0 })
        {
            foreach (var forbidden in forbiddenTools)
            {
                if (actualToolSet.Contains(forbidden))
                {
                    failures.Add(new AssertionFailure(
                        $"Forbidden tool '{forbidden}' was called",
                        $"This tool should not have been invoked"));
                }
            }
        }

        // Check custom tool predicates
        if (toolPredicates is { Count: > 0 })
        {
            foreach (var predicate in toolPredicates)
            {
                var matched = runResult.ToolInvocations.Any(predicate);
                if (!matched)
                {
                    failures.Add(new AssertionFailure(
                        "A custom tool predicate was not satisfied"));
                }
            }
        }

        return failures;
    }

    internal static List<AssertionFailure> CheckGraderResult(
        GraderResult? graderResult,
        double? minimumCoverage,
        double? minimumConfidence)
    {
        var failures = new List<AssertionFailure>();
        if (graderResult is null)
        {
            failures.Add(new AssertionFailure(
                "Grader did not return a result",
                "No semantic grading information is available"));
            return failures;
        }

        if (!graderResult.Passed)
        {
            foreach (var unmet in graderResult.UnmetCriteria)
            {
                failures.Add(new AssertionFailure(
                    $"Essential state not met: '{unmet}'",
                    graderResult.Reasoning));
            }

            if (graderResult.UnmetCriteria.Length == 0)
            {
                failures.Add(new AssertionFailure(
                    "Semantic grading failed",
                    graderResult.Reasoning));
            }
        }

        if (minimumCoverage.HasValue && graderResult.Coverage < minimumCoverage.Value)
        {
            failures.Add(new AssertionFailure(
                $"Coverage {graderResult.Coverage:P0} is below minimum {minimumCoverage.Value:P0}",
                $"Met: [{string.Join(", ", graderResult.MetCriteria)}], " +
                $"Unmet: [{string.Join(", ", graderResult.UnmetCriteria)}]"));
        }

        if (minimumConfidence.HasValue && graderResult.Confidence < minimumConfidence.Value)
        {
            failures.Add(new AssertionFailure(
                $"Confidence {graderResult.Confidence:F2} is below minimum {minimumConfidence.Value:F2}",
                graderResult.Reasoning));
        }

        return failures;
    }

    internal static void ThrowIfFailed(
        List<AssertionFailure> failures,
        GraderResult? graderResult,
        AgentRunResult? runResult,
        double? minimumCoverage)
    {
        if (failures.Count == 0)
            return;

        var toolInvocations = runResult?.ToolInvocations;

        // Determine the most specific exception type
        var hasToolFailures = failures.Any(f =>
            f.Description.Contains("tool", StringComparison.OrdinalIgnoreCase) &&
            (f.Description.Contains("not called", StringComparison.OrdinalIgnoreCase) ||
             f.Description.Contains("not all called", StringComparison.OrdinalIgnoreCase)));

        var hasCoverageFailure = failures.Any(f =>
            f.Description.Contains("Coverage", StringComparison.OrdinalIgnoreCase) &&
            f.Description.Contains("below", StringComparison.OrdinalIgnoreCase));

        var hasStateFailures = failures.Any(f =>
            f.Description.Contains("Essential state", StringComparison.OrdinalIgnoreCase));

        if (hasToolFailures)
        {
            var expected = failures
                .Where(f => f.Description.StartsWith("Expected tool"))
                .Select(f => f.Description.Split('\'')[1])
                .ToList();
            var actual = runResult?.ToolInvocations
                .Select(i => i.ToolName)
                .Distinct()
                .ToList() ?? [];

            throw new ToolNotCalledException(expected, actual, failures, toolInvocations);
        }

        if (hasCoverageFailure && minimumCoverage.HasValue)
        {
            throw new CoverageThresholdException(
                graderResult?.Coverage ?? 0.0,
                minimumCoverage.Value,
                failures,
                graderResult,
                toolInvocations);
        }

        if (hasStateFailures)
        {
            var missedStates = graderResult?.UnmetCriteria.ToList()
                ?? failures
                    .Where(f => f.Description.Contains("Essential state"))
                    .Select(f => f.Description)
                    .ToList();

            throw new EssentialStateMissedException(
                missedStates, failures, graderResult, toolInvocations);
        }

        // Generic failure
        var message = string.Join("; ", failures.Select(f => f.Description));
        throw new SemanticAssertionException(message, failures, graderResult, toolInvocations);
    }
}
