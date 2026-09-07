using Microsoft.Extensions.Logging;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Providers;

namespace NativeTavern.Services;

public sealed class ChatService(
    ChatSessionRepository sessionRepository,
    ChatMessageRepository messageRepository,
    CharacterRepository characterRepository,
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

    public async Task<ChatSession> CreateSessionAsync(Character? character = null)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSession
        {
            Title = character?.Name ?? "New Chat",
            CharacterId = character?.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        await sessionRepository.CreateAsync(session);
        if (character is not null && !string.IsNullOrWhiteSpace(character.FirstMessage))
        {
            await messageRepository.AddAsync(new ChatMessage
            {
                ChatSessionId = session.Id,
                Role = ChatRole.Assistant,
                Content = RenderCharacterText(character.FirstMessage, character.Name),
                CreatedAt = now
            });
        }
        return session;
    }

    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(long sessionId) =>
        messageRepository.GetBySessionAsync(sessionId);

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

        Character? characterContext = null;
        if (session.CharacterId is long characterId)
            characterContext = await characterRepository.GetAsync(characterId);

        IEnumerable<ChatMessage> requestHistory = history;
        if (characterContext is not null && !settings.IncludeCharacterContext &&
            history.FirstOrDefault() is { Role: ChatRole.Assistant } greeting &&
            greeting.Content == RenderCharacterText(characterContext.FirstMessage, characterContext.Name))
        {
            requestHistory = history.Skip(1);
        }

        var requestMessages = BuildMessages(requestHistory).ToList();
        if (characterContext is not null && settings.IncludeCharacterContext)
        {
            requestMessages.Insert(0, new ChatCompletionMessage
            {
                Role = "system",
                Content = BuildCharacterContext(characterContext)
            });
        }

        var request = new ChatCompletionRequest
        {
            Model = settings.Model,
            Messages = requestMessages,
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

    private static string BuildCharacterContext(Character character)
    {
        var sections = new[]
        {
            $"You are {character.Name}. Stay in character throughout the conversation.",
            string.IsNullOrWhiteSpace(character.Description) ? null : "Description:" + Environment.NewLine + character.Description,
            string.IsNullOrWhiteSpace(character.Personality) ? null : "Personality:" + Environment.NewLine + character.Personality,
            string.IsNullOrWhiteSpace(character.Scenario) ? null : "Scenario:" + Environment.NewLine + character.Scenario,
            string.IsNullOrWhiteSpace(character.ExampleMessages) ? null : "Example dialogue:" + Environment.NewLine + character.ExampleMessages
        };
        return RenderCharacterText(
            string.Join(Environment.NewLine + Environment.NewLine, sections.Where(x => x is not null)),
            character.Name);
    }

    private static string RenderCharacterText(string text, string characterName) =>
        text.Replace("{{char}}", characterName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{user}}", "User", StringComparison.OrdinalIgnoreCase);
}
