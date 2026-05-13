namespace SemanticAssertions.Assertions;

/// <summary>
/// Builder for starting a multi-turn conversation. Call UserSays() to begin.
/// </summary>
public sealed class ConversationBuilder
{
    private readonly List<string> _userMessages = [];

    /// <summary>
    /// Start the conversation with the first user message.
    /// </summary>
    public ConversationContinuationBuilder UserSays(string message)
    {
        _userMessages.Add(message);
        return new ConversationContinuationBuilder(_userMessages);
    }
}

/// <summary>
/// Continuation builder for adding follow-up messages after the initial UserSays().
/// </summary>
public sealed class ConversationContinuationBuilder
{
    private readonly List<string> _userMessages;

    internal ConversationContinuationBuilder(List<string> userMessages)
    {
        _userMessages = userMessages;
    }

    /// <summary>
    /// Add a follow-up user message to the conversation.
    /// </summary>
    public ConversationContinuationBuilder ThenUserSays(string message)
    {
        _userMessages.Add(message);
        return this;
    }

    internal IReadOnlyList<string> Build() => _userMessages;
}

