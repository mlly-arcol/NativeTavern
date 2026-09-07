using Microsoft.Extensions.Logging;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Providers;

namespace NativeTavern.Services;

public sealed class ChatService(
    ChatSessionRepository sessionRepository,
    ChatMessageRepository messageRepository,
    SettingsService settingsService,
    ILLMProvider provider,
    ILogger<ChatService> logger)
{
    public async Task<(ChatSession Session, IReadOnlyList<ChatMessage> Messages)> LoadLatestAsync()
    {
        var sessions = await sessionRepository.GetAllAsync();
        var session = sessions.FirstOrDefault() ?? await CreateSessionAsync();
        return (session, await messageRepository.GetBySessionAsync(session.Id));
    }

    public async Task<ChatSession> CreateSessionAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSession { CreatedAt = now, UpdatedAt = now };
        await sessionRepository.CreateAsync(session);
        return session;
    }

    public async Task<ChatMessage> SendAsync(
        ChatSession session,
        string input,
        Func<ChatMessage, ChatMessage, Task> onStarted,
        Func<ChatMessage, string, Task> onChunk,
        CancellationToken cancellationToken)
    {
        var settings = await settingsService.LoadAsync();
        if (!settings.IsConfigured) throw new InvalidOperationException("尚未配置模型 Provider，请先前往 Settings。");

        var now = DateTimeOffset.UtcNow;
        var user = new ChatMessage
        {
            ChatSessionId = session.Id, Role = ChatRole.User,
            Content = input.Trim(), CreatedAt = now
        };
        await messageRepository.AddAsync(user);

        var history = await messageRepository.GetBySessionAsync(session.Id);
        var assistant = new ChatMessage
        {
            ChatSessionId = session.Id, Role = ChatRole.Assistant,
            Content = string.Empty, CreatedAt = DateTimeOffset.UtcNow
        };
        await messageRepository.AddAsync(assistant);
        await onStarted(user, assistant);

        if (history.Count == 1)
        {
            session.Title = user.Content.Length > 32 ? user.Content[..32] + "…" : user.Content;
            session.UpdatedAt = now;
            await sessionRepository.UpdateAsync(session);
        }

        var request = new ChatCompletionRequest
        {
            Model = settings.Model,
            Messages = BuildMessages(history),
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            MaxTokens = settings.MaxTokens,
            Stream = true
        };

        try
        {
            await foreach (var chunk in provider.StreamAsync(request, cancellationToken))
            {
                assistant.Content += chunk;
                await onChunk(assistant, chunk);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Generation cancelled by user.");
        }
        finally
        {
            assistant.UpdatedAt = DateTimeOffset.UtcNow;
            await messageRepository.UpdateAsync(assistant);
            session.UpdatedAt = assistant.UpdatedAt.Value;
            await sessionRepository.UpdateAsync(session);
        }
        return assistant;
    }

    public static IReadOnlyList<ChatCompletionMessage> BuildMessages(IEnumerable<ChatMessage> messages) =>
        messages.Select(message => new ChatCompletionMessage
        {
            Role = message.Role.ToString().ToLowerInvariant(),
            Content = message.Content
        }).ToList();
}
