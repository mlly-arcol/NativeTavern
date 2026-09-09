using System.IO.Compression;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

public sealed class PluginServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "NativeTavern-plugin-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeCreatesAnEmptyCatalog()
    {
        var service = CreateService();

        await service.InitializeAsync();
        var catalog = await service.GetCatalogAsync();

        Assert.Empty(catalog);
        Assert.True(File.Exists(Path.Combine(root, "catalog.json")));
    }

    [Fact]
    public async Task InstallsTogglesAndUninstallsPackage()
    {
        var package = CreatePackage("sample.plugin", "1.2.0");
        var service = CreateService();

        var installed = await service.InstallPackageAsync(package);
        Assert.Equal("sample.plugin", installed.Id);
        Assert.True(File.Exists(Path.Combine(root, "sample.plugin", "plugin.json")));
        Assert.Single(await service.GetInstalledAsync());

        await service.SetEnabledAsync(installed.Id, false);
        Assert.False(Assert.Single(await service.GetInstalledAsync()).IsEnabled);

        await service.UninstallAsync(installed.Id);
        Assert.Empty(await service.GetInstalledAsync());
    }

    [Fact]
    public async Task UpdatingPackageReplacesOldFiles()
    {
        var service = CreateService();
        await service.InstallPackageAsync(CreatePackage("sample.plugin", "1.0.0", ("old.txt", "old")));

        await service.InstallPackageAsync(CreatePackage("sample.plugin", "2.0.0", ("new.txt", "new")));

        var installed = Assert.Single(await service.GetInstalledAsync());
        Assert.Equal("2.0.0", installed.Version);
        Assert.False(File.Exists(Path.Combine(root, "sample.plugin", "old.txt")));
        Assert.True(File.Exists(Path.Combine(root, "sample.plugin", "new.txt")));
    }

    [Fact]
    public async Task RejectsUnsafeArchivePaths()
    {
        var package = Path.Combine(root, Guid.NewGuid().ToString("N") + ".ntplugin");
        Directory.CreateDirectory(root);
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "plugin.json", Manifest("safe.plugin", "1.0.0"));
            WriteEntry(archive, "../outside.txt", "unsafe");
        }

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => CreateService().InstallPackageAsync(package));
        Assert.Contains("不安全", error.Message);
        Assert.False(File.Exists(Path.Combine(Directory.GetParent(root)!.FullName, "outside.txt")));
    }

    [Fact]
    public async Task MarketplaceInstallVerifiesAndInstallsDownloadedPackage()
    {
        var bytes = await File.ReadAllBytesAsync(CreatePackage("store.plugin", "1.0.0"));
        using var client = new HttpClient(new StaticResponseHandler(bytes));
        var service = new PluginService(root, client, NullLogger<PluginService>.Instance);
        var listing = new MarketplacePlugin
        {
            Id = "store.plugin",
            Name = "Store Plugin",
            Version = "1.0.0",
            PackageUrl = "https://plugins.example/store.ntplugin",
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes))
        };

        var installed = await service.InstallFromMarketplaceAsync(listing);

        Assert.Equal(listing.Id, installed.Id);
        Assert.True(File.Exists(Path.Combine(root, listing.Id, "plugin.json")));
    }

    [Theory]
    [InlineData("../bad")]
    [InlineData("UPPERCASE")]
    [InlineData("a")]
    public async Task RejectsInvalidPluginIds(string id)
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateService().InstallPackageAsync(CreatePackage(id, "1.0.0")));
        Assert.Contains("插件 ID", error.Message);
    }

    private PluginService CreateService() => new(root, NullLogger<PluginService>.Instance);

    private string CreatePackage(string id, string version, params (string Name, string Content)[] files)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, Guid.NewGuid().ToString("N") + ".ntplugin");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "plugin.json", Manifest(id, version));
        foreach (var file in files) WriteEntry(archive, file.Name, file.Content);
        return path;
    }

    private static string Manifest(string id, string version) =>
        $$"""{"id":"{{id}}","name":"Sample","version":"{{version}}","author":"Tests","description":"Test plugin","permissions":[]}""";

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed class StaticResponseHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content),
                RequestMessage = request
            });
    }
}
