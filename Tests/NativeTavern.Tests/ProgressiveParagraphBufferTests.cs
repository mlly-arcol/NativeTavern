using NativeTavern.Helpers;
using Xunit;

namespace NativeTavern.Tests;

public sealed class ProgressiveParagraphBufferTests
{
    [Fact]
    public void EmitsOnlyAfterACompleteParagraphArrives()
    {
        var buffer = new ProgressiveParagraphBuffer();

        Assert.Empty(buffer.Append("第一段还没结束"));
        Assert.Equal("第一段还没结束。\n\n", Assert.Single(buffer.Append("。\n\n第二段")));
        Assert.Equal("第二段", Assert.Single(buffer.Flush()));
    }

    [Fact]
    public void LongParagraphCanProgressAtASentenceBoundary()
    {
        var buffer = new ProgressiveParagraphBuffer();
        var prefix = new string('字', 520);

        Assert.Equal(prefix + "。", Assert.Single(buffer.Append(prefix + "。后续内容")));
        Assert.Equal("后续内容", Assert.Single(buffer.Flush()));
    }
}
