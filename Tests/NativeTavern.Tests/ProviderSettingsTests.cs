using NativeTavern.Models;
using Xunit;

namespace NativeTavern.Tests;

public sealed class ProviderSettingsTests
{
    [Theory]
    [InlineData("https://api.example.com/v1", true)]
    [InlineData("http://127.0.0.1:8080/v1", true)]
    [InlineData("file:///C:/private.txt", false)]
    [InlineData("ftp://example.com/models", false)]
    [InlineData("not-a-url", false)]
    public void BaseUrlOnlyAcceptsHttpSchemes(string value, bool expected)
    {
        Assert.Equal(expected, ProviderSettings.IsValidBaseUrl(value));
    }
}
