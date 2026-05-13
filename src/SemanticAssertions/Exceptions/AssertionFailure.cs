namespace SemanticAssertions.Exceptions;

/// <summary>
/// Represents a single assertion failure with a description and optional detail.
/// </summary>
public sealed record AssertionFailure(string Description, string? Detail = null);
