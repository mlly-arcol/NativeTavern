using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Helpers;
using NativeTavern.Services;
using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

public class KnowledgeImportTests
{
    [Fact]
    public async Task BatchContinuesAfterInvalidFileAndDisplaysSuccessfulImports()
    {
        var previousRoot = AppPaths.Root;
        var directory = Path.Combine(Path.GetTempPath(), "NativeTavernKnowledge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            AppPaths.UseRoot(directory);
            AppPaths.EnsureCreated();
            var factory = new DatabaseConnectionFactory(Path.Combine(directory, "test.db"));
            await new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance).InitializeAsync();
            var service = new KnowledgeService(new KnowledgeRepository(factory), NullLogger<KnowledgeService>.Instance);
            var vm = new KnowledgeViewModel(service, NullLogger<KnowledgeViewModel>.Instance);
            var first = Path.Combine(directory, "first.txt");
            var second = Path.Combine(directory, "second.md");
            var invalid = Path.Combine(directory, "invalid.xyz");
            await File.WriteAllTextAsync(first, "First document content");
            await File.WriteAllTextAsync(second, "Second document content");
            await File.WriteAllTextAsync(invalid, "Unsupported file");

            await vm.ImportFilesAsync([first, invalid, second, first]);

            Assert.Equal(2, vm.Documents.Count);
            Assert.Equal(2, (await service.GetDocumentsAsync()).Count);
            Assert.Contains("2/3", vm.StatusMessage);
            Assert.Contains("invalid.xyz", vm.StatusMessage);
            Assert.False(vm.IsBusy);
        }
        finally
        {
            AppPaths.UseRoot(previousRoot);
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }
}
