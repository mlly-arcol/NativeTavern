using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

public sealed class ReplySuggestionServiceTests
{
    [Fact]
    public void ParsesExactlyThreeSuggestions()
    {
        var result = ReplySuggestionService.ParseResponse(
            "```json\n{\"suggestions\":[\"追问钥匙的来历。\",\"观察她的表情。\",\"先答应同行。\",\"多余选项\"]}\n```");

        Assert.Equal(3, result.Count);
        Assert.Equal("追问钥匙的来历。", result[0]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"suggestions\":[]}")]
    public void InvalidOrEmptyResponseReturnsNoSuggestions(string response) =>
        Assert.Empty(ReplySuggestionService.ParseResponse(response));

    [Fact]
    public void FallbackAlwaysCompletesThreePlotAwareSuggestions()
    {
        var result = ReplySuggestionService.CompleteSuggestions(
            ["我先问问钥匙是谁留下的。"],
            [new ChatMessage { Role = ChatRole.Assistant, Content = "她把生锈的钥匙放在桌面上。" }]);

        Assert.Equal(3, result.Count);
        Assert.Contains("钥匙", result[1]);
    }
}
