using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Providers;
using NativeTavern.Security;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task ProviderFailureBeforeFirstChunkDoesNotLeaveBlankAssistantMessage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NativeTavernChatTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = Path.Combine(directory, "chat.db");
            var (service, messages) = await CreateServiceAsync(database, new FailingProvider());
            var session = await service.CreateSessionAsync();

            await Assert.ThrowsAsync<ProviderException>(() => service.SendAsync(
                session,
                "保留用户消息",
                [],
                (_, _) => Task.CompletedTask,
                (_, _) => Task.CompletedTask,
                CancellationToken.None));

            var stored = await messages.GetBySessionAsync(session.Id);
            var user = Assert.Single(stored);
            Assert.Equal(ChatRole.User, user.Role);
            Assert.Equal("保留用户消息", user.Content);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    private static async Task<(ChatService Service, ChatMessageRepository Messages)> CreateServiceAsync(
        string database,
        ILLMProvider provider)
    {
        var factory = new DatabaseConnectionFactory(database);
        await new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance).InitializeAsync();
        var sessions = new ChatSessionRepository(factory);
        var messages = new ChatMessageRepository(factory);
        var swipes = new MessageSwipeRepository(factory);
        var attachments = new ChatAttachmentRepository(factory);
        var characters = new CharacterRepository(factory);
        var prompts = new PromptRepository(factory);
        var knowledgeRepository = new KnowledgeRepository(factory);
        var settings = new SettingsService(new SettingsRepository(factory), new PassthroughProtector());
        await settings.SaveAsync(new ProviderSettings
        {
            BaseUrl = "http://127.0.0.1:8080/v1",
            Model = "test-model"
        }, null);
        var knowledge = new KnowledgeService(knowledgeRepository, NullLogger<KnowledgeService>.Instance);
        var promptService = new PromptService(prompts, characters, sessions, knowledge);
        var attachmentService = new AttachmentService(attachments, NullLogger<AttachmentService>.Instance);
        var summary = new ConversationSummaryService(messages, sessions);
        return (new ChatService(
            sessions, messages, swipes, attachments, attachmentService, summary, characters,
            promptService, settings, provider, NullLogger<ChatService>.Instance), messages);
    }

    private sealed class FailingProvider : ILLMProvider
    {
        public string Id => "test";
        public string DisplayName => "Test";
        public Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModelInfo>>([]);
        public async IAsyncEnumerable<string> StreamAsync(
            ChatCompletionRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new ProviderException("模拟服务故障。");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class PassthroughProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;
        public string Unprotect(string protectedText) => protectedText;
    }
}
