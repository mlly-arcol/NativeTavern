using NativeTavern.Models;
using NativeTavern.Providers;

namespace NativeTavern.Services;

public sealed class ProviderDiscoveryService(ProviderRouter router)
{
    public async Task<IReadOnlyList<DetectedProvider>> ScanLocalAsync(CancellationToken cancellationToken)
    {
        var profiles = ProviderProfile.All.Where(x => x.IsLocal);
        var tasks = profiles.Select(x => ProbeAsync(x, cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.Where(x => x is not null).Cast<DetectedProvider>().ToList();
    }

    private async Task<DetectedProvider?> ProbeAsync(
        ProviderProfile profile, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(1400));
        try
        {
            var settings = new ProviderSettings
            {
                ProviderId = profile.Id,
                BaseUrl = profile.DefaultBaseUrl,
                Model = "local-scan"
            };
            var models = await router.GetModelsAsync(settings, string.Empty, timeout.Token);
            return models.Count == 0 ? null : new DetectedProvider(profile, models);
        }
        catch (Exception ex) when (ex is ProviderException or OperationCanceledException)
        {
            return null;
        }
    }
}
