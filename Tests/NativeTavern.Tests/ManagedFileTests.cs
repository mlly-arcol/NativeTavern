using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Helpers;
using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

public sealed class ManagedFileTests
{
    [Fact]
    public void DirectoryGuardRejectsSiblingAndParentPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "managed", "files");

        Assert.True(ManagedFile.IsInsideDirectory(Path.Combine(root, "image.png"), root));
        Assert.False(ManagedFile.IsInsideDirectory(Path.Combine(root, "..", "secret.png"), root));
        Assert.False(ManagedFile.IsInsideDirectory(Path.Combine(Path.GetTempPath(), "managed", "files-old", "image.png"), root));
    }

    [Fact]
    public async Task ImageEncodingIgnoresFilesOutsideManagedAttachmentDirectory()
    {
        var external = Path.Combine(Path.GetTempPath(), "NativeTavern-external-" + Guid.NewGuid().ToString("N") + ".png");
        await File.WriteAllBytesAsync(external, [1, 2, 3]);
        try
        {
            var result = await AttachmentService.ToDataUrlsAsync([
                new ChatAttachment { FilePath = external, MimeType = "image/png" }
            ]);

            Assert.Empty(result);
        }
        finally
        {
            ManagedFile.TryDelete(external, Path.GetTempPath(), NullLogger.Instance);
        }
    }
}
