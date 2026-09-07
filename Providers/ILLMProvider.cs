using NativeTavern.Models;

namespace NativeTavern.Providers;

public interface ILLMProvider
{
    string Id { get; }
    string DisplayName { get; }
    IAsyncEnumerable<string> StreamAsync(ChatCompletionRequest request, CancellationToken cancellationToken);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken);
}
