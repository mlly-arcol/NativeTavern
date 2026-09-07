using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

public sealed class LocalModelServiceTests
{
    [Fact]
    public void Scan_ReturnsOnlyTopLevelGgufFilesInNameOrder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NativeTavern-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "nested"));
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "b.gguf"), new byte[8]);
            File.WriteAllBytes(Path.Combine(directory, "a.GGUF"), new byte[4]);
            File.WriteAllText(Path.Combine(directory, "ignored.txt"), "not a model");
            File.WriteAllBytes(Path.Combine(directory, "nested", "nested.gguf"), new byte[2]);

            using var service = new LocalModelService();
            var models = service.Scan(directory);

            Assert.Equal(["a.GGUF", "b.gguf"], models.Select(x => x.Name));
            Assert.Equal([4L, 8L], models.Select(x => x.SizeBytes));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Scan_MissingDirectory_ReturnsEmptyList()
    {
        using var service = new LocalModelService();
        Assert.Empty(service.Scan(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}
