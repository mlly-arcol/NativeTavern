using NativeTavern.Models;
using NativeTavern.Providers;
using Xunit;

namespace NativeTavern.Tests;

public sealed class ClaudeProviderTests
{
    [Fact]
    public void NormalizeMessagesPrependsUserTurnWhenHistoryStartsWithAssistant()
    {
        var messages = new[]
        {
            new ChatCompletionMessage { Role = "assistant", Content = "你好，旅行者。" },
            new ChatCompletionMessage { Role = "user", Content = "嗨" }
        };

        var normalized = ClaudeProvider.NormalizeMessages(messages);

        Assert.Equal(3, normalized.Count);
        Assert.Equal("user", normalized[0].Role);
        Assert.Equal("assistant", normalized[1].Role);
        Assert.Equal("你好，旅行者。", normalized[1].Content);
        Assert.Equal("user", normalized[2].Role);
    }

    [Fact]
    public void NormalizeMessagesKeepsHistoryThatStartsWithUser()
    {
        var messages = new[]
        {
            new ChatCompletionMessage { Role = "user", Content = "嗨" },
            new ChatCompletionMessage { Role = "assistant", Content = "你好" }
        };

        var normalized = ClaudeProvider.NormalizeMessages(messages);

        Assert.Equal(2, normalized.Count);
        Assert.Equal("user", normalized[0].Role);
        Assert.Equal("嗨", normalized[0].Content);
        Assert.Equal("assistant", normalized[1].Role);
    }

    [Fact]
    public void NormalizeMessagesMergesConsecutiveSameRoleText()
    {
        var messages = new[]
        {
            new ChatCompletionMessage { Role = "assistant", Content = "A" },
            new ChatCompletionMessage { Role = "assistant", Content = "B" },
            new ChatCompletionMessage { Role = "user", Content = "C" }
        };

        var normalized = ClaudeProvider.NormalizeMessages(messages);

        Assert.Equal(3, normalized.Count);
        Assert.Equal("user", normalized[0].Role);
        Assert.Equal("assistant", normalized[1].Role);
        Assert.Equal("A\n\nB", normalized[1].Content);
        Assert.Equal("user", normalized[2].Role);
        Assert.Equal("C", normalized[2].Content);
    }
}
