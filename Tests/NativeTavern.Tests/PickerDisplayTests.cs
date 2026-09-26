using NativeTavern.Models;
using Xunit;

namespace NativeTavern.Tests;

/// <summary>
/// The conversation picker and the search popup render their items through templates that fall back to
/// ToString, so a model without a readable override leaks its type name into the UI and to screen readers.
/// </summary>
public class PickerDisplayTests
{
    [Fact]
    public void SessionPickerTextCarriesPinAndGroup()
    {
        var session = new ChatSession { Title = "界碑之外", GroupName = "测试篇", IsPinned = true };
        Assert.Equal("📌 界碑之外 · 测试篇", session.PickerText);
        Assert.Equal("界碑之外", new ChatSession { Title = "界碑之外" }.PickerText);
    }

    [Fact]
    public void SearchResultToStringIsReadable()
    {
        var result = new MessageSearchResult { SessionTitle = "界碑之外", Speaker = "守墓人", Snippet = "这里的碑一个都还没倒。" };
        Assert.Equal(result.Label, result.ToString());
        Assert.DoesNotContain("NativeTavern.Models", result.ToString());
    }
}
