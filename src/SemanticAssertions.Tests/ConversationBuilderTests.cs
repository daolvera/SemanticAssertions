using SemanticAssertions.Assertions;

namespace SemanticAssertions.Tests;

public class ConversationBuilderTests
{
    [Fact]
    public void UserSays_ReturnsConversationContinuationBuilder()
    {
        var builder = new ConversationBuilder();

        var continuation = builder.UserSays("Hello");

        Assert.IsType<ConversationContinuationBuilder>(continuation);
    }

    [Fact]
    public void ThenUserSays_ChainsOnContinuationBuilder()
    {
        var builder = new ConversationBuilder();

        var continuation = builder
            .UserSays("First message")
            .ThenUserSays("Second message")
            .ThenUserSays("Third message");

        Assert.IsType<ConversationContinuationBuilder>(continuation);
    }

    [Fact]
    public void Build_ReturnsSingleMessage_WhenOnlyUserSaysCalled()
    {
        var builder = new ConversationBuilder();

        var continuation = builder.UserSays("Only message");
        var messages = continuation.Build();

        Assert.Single(messages);
        Assert.Equal("Only message", messages[0]);
    }

    [Fact]
    public void Build_ReturnsAllMessagesInOrder()
    {
        var builder = new ConversationBuilder();

        var messages = builder
            .UserSays("First")
            .ThenUserSays("Second")
            .ThenUserSays("Third")
            .Build();

        Assert.Equal(3, messages.Count);
        Assert.Equal("First", messages[0]);
        Assert.Equal("Second", messages[1]);
        Assert.Equal("Third", messages[2]);
    }

    [Fact]
    public void ConversationBuilder_DoesNotExposeThenUserSays()
    {
        // Verify at the type level that ConversationBuilder does NOT have ThenUserSays
        var methods = typeof(ConversationBuilder).GetMethods()
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain("ThenUserSays", methods);
    }

    [Fact]
    public void ConversationContinuationBuilder_DoesNotExposeUserSays()
    {
        // Verify at the type level that ConversationContinuationBuilder does NOT have UserSays
        var methods = typeof(ConversationContinuationBuilder).GetMethods()
            .Select(m => m.Name)
            .ToList();

        Assert.DoesNotContain("UserSays", methods);
    }
}
