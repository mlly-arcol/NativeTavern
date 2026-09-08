using Microsoft.Extensions.Logging;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Providers;

namespace NativeTavern.Services;

public sealed class ChatService(
    ChatSessionRepository sessionRepository,
    ChatMessageRepository messageRepository,
    MessageSwipeRepository swipeRepository,
    ChatAttachmentRepository attachmentRepository,
    AttachmentService attachmentService,
    ConversationSummaryService summaryService,
    CharacterRepository characterRepository,
    PromptService promptService,
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

    public Task<IReadOnlyList<ChatSession>> GetSessionsAsync() => sessionRepository.GetAllAsync();

    public async Task<IReadOnlyList<Character>> GetGroupMembersAsync(long sessionId) =>
        await characterRepository.GetByIdsAsync(await sessionRepository.GetCharacterIdsAsync(sessionId));

    public Task<IReadOnlyList<Character>> GetAllCharactersAsync() =>
        characterRepository.SearchAsync(string.Empty, false);

    public async Task<ChatSession> CreateGroupSessionAsync(string groupName)
    {
        var members = await characterRepository.GetByGroupAsync(groupName);
        if (members.Count < 2) throw new InvalidOperationException("群聊至少需要两个角色。");
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSession
        {
            Title = groupName.Trim(),
            IsGroupChat = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        await sessionRepository.CreateAsync(session);
        await sessionRepository.SetCharactersAsync(session.Id, members.Select(x => x.Id).ToList());
        return session;
    }

    public async Task UpdateGroupSessionAsync(
        ChatSession session,
        string title,
        IReadOnlyCollection<long> characterIds)
    {
        var ids = characterIds.Distinct().ToList();
        if (ids.Count < 2) throw new InvalidOperationException("群聊至少需要两个角色。");
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("群聊名称不能为空。");
        session.Title = title.Trim();
        session.IsGroupChat = true;
        session.CharacterId = null;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await sessionRepository.SetCharactersAsync(session.Id, ids);
        await sessionRepository.UpdateAsync(session);
    }

    public async Task<ChatSession> CreateSessionAsync(Character? character = null)
    {
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSession
        {
            Title = character?.Name ?? "New Chat", CharacterId = character?.Id,
            CreatedAt = now, UpdatedAt = now
        };
        await sessionRepository.CreateAsync(session);
        if (character is not null && !string.IsNullOrWhiteSpace(character.FirstMessage))
        {
            var greeting = new ChatMessage
            {
                ChatSessionId = session.Id, Role = ChatRole.Assistant,
                Content = RenderCharacterText(character.FirstMessage, character.Name), CreatedAt = now
            };
            await messageRepository.AddAsync(greeting);
            await swipeRepository.AddAsync(new MessageSwipe
            {
                ChatMessageId = greeting.Id, SwipeIndex = 0,
                Content = greeting.Content, CreatedAt = now
            });
        }
        return session;
    }

    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(long sessionId) =>
        messageRepository.GetBySessionAsync(sessionId);

    public Task<Character?> GetCharacterAsync(long characterId) => characterRepository.GetAsync(characterId);

    public Task<IReadOnlyList<ChatAttachment>> GetAttachmentsAsync(long messageId) => attachmentRepository.GetByMessageAsync(messageId);

    public async Task<PromptBuildResult> GetPromptPreviewAsync(ChatSession session)
    {
        var settings = await LoadConfiguredSettingsAsync();
        return await promptService.BuildAsync(session, await messageRepository.GetBySessionAsync(session.Id), settings);
    }

    public async Task<int> GetSwipeCountAsync(ChatMessage message) =>
        message.Role == ChatRole.Assistant ? (await EnsureSwipeHistoryAsync(message)).Count : 0;

    public Task DeleteSessionAsync(long id) => sessionRepository.DeleteAsync(id);
    public Task DeleteMessageAsync(long id) => messageRepository.DeleteAsync(id);

    public async Task UpdateSessionPromptAsync(
        ChatSession session, long? personaId, long? lorebookId, long? presetId, string authorNote)
    {
        session.PersonaId = personaId;
        session.LorebookId = lorebookId;
        session.PromptPresetId = presetId;
        session.AuthorNote = authorNote;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await sessionRepository.UpdateAsync(session);
    }

    public async Task UpdateMessageAsync(ChatMessage message)
    {
        message.Content = message.Content.Trim();
        message.UpdatedAt = DateTimeOffset.UtcNow;
        if (message.Role == ChatRole.Assistant)
        {
            message.CurrentSwipeIndex = 0;
            await swipeRepository.ReplaceWithSingleAsync(message.Id, message.Content);
        }
        await messageRepository.UpdateAsync(message);
    }

    public async Task<(ChatMessage Message, int SwipeCount)> SelectSwipeAsync(ChatMessage message, int targetIndex)
    {
        var swipes = await EnsureSwipeHistoryAsync(message);
        if (targetIndex < 0 || targetIndex >= swipes.Count)
            throw new ArgumentOutOfRangeException(nameof(targetIndex));
        message.CurrentSwipeIndex = targetIndex;
        message.Content = swipes[targetIndex].Content;
        message.UpdatedAt = DateTimeOffset.UtcNow;
        await messageRepository.UpdateAsync(message);
        return (message, swipes.Count);
    }

    public async Task<ChatMessage> SendAsync(
        ChatSession session, string input, IReadOnlyList<string> imagePaths,
        Func<ChatMessage, ChatMessage, Task> onStarted,
        Func<ChatMessage, string, Task> onChunk,
        CancellationToken cancellationToken)
    {
        var settings = await LoadConfiguredSettingsAsync();
        var now = DateTimeOffset.UtcNow;
        var user = new ChatMessage
        {
            ChatSessionId = session.Id, Role = ChatRole.User,
            Content = input.Trim(), CreatedAt = now
        };
        await messageRepository.AddAsync(user);
        if (imagePaths.Count > 0) await attachmentService.SaveImagesAsync(user.Id, imagePaths);
        var history = await messageRepository.GetBySessionAsync(session.Id);
        var speaker = await SelectSpeakerAsync(session, history);
        var assistant = new ChatMessage
        {
            ChatSessionId = session.Id, Role = ChatRole.Assistant,
            SpeakerCharacterId = speaker?.Id,
            Content = string.Empty, CreatedAt = DateTimeOffset.UtcNow
        };
        await messageRepository.AddAsync(assistant);
        await onStarted(user, assistant);

        if (history.Count == 1 && !session.IsGroupChat)
        {
            session.Title = user.Content.Length > 32 ? user.Content[..32] + "…" : user.Content;
            session.UpdatedAt = now;
            await sessionRepository.UpdateAsync(session);
        }

        var request = await CreateRequestAsync(session, history, settings, assistant.SpeakerCharacterId);
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
            if (!string.IsNullOrEmpty(assistant.Content))
            {
                await swipeRepository.AddAsync(new MessageSwipe
                {
                    ChatMessageId = assistant.Id, SwipeIndex = 0,
                    Content = assistant.Content, CreatedAt = assistant.UpdatedAt.Value
                });
            }
            session.UpdatedAt = assistant.UpdatedAt.Value;
            await sessionRepository.UpdateAsync(session);
            try { await summaryService.UpdateIfNeededAsync(session); }
            catch (Exception ex) { logger.LogWarning(ex, "Automatic summary update failed."); }
        }
        return assistant;
    }

    public async Task<ChatMessage> GenerateNextSpeakerAsync(
        ChatSession session,
        Func<ChatMessage, Task> onStarted,
        Func<ChatMessage, string, Task> onChunk,
        CancellationToken cancellationToken)
    {
        if (!session.IsGroupChat) throw new InvalidOperationException("当前会话不是群聊。");
        var settings = await LoadConfiguredSettingsAsync();
        var history = await messageRepository.GetBySessionAsync(session.Id);
        var speaker = await SelectSpeakerAsync(session, history)
                      ?? throw new InvalidOperationException("群聊没有可用角色。");
        var assistant = new ChatMessage
        {
            ChatSessionId = session.Id,
            Role = ChatRole.Assistant,
            SpeakerCharacterId = speaker.Id,
            Content = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await messageRepository.AddAsync(assistant);
        await onStarted(assistant);
        var request = await CreateRequestAsync(session, history, settings, speaker.Id);
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
            logger.LogInformation("Group chat generation cancelled by user.");
        }
        finally
        {
            assistant.UpdatedAt = DateTimeOffset.UtcNow;
            await messageRepository.UpdateAsync(assistant);
            if (!string.IsNullOrEmpty(assistant.Content))
                await swipeRepository.AddAsync(new MessageSwipe
                {
                    ChatMessageId = assistant.Id,
                    SwipeIndex = 0,
                    Content = assistant.Content,
                    CreatedAt = assistant.UpdatedAt.Value
                });
            session.UpdatedAt = assistant.UpdatedAt.Value;
            await sessionRepository.UpdateAsync(session);
        }
        return assistant;
    }

    public async Task<(ChatMessage Message, int SwipeCount)> GenerateAlternativeAsync(
        ChatSession session, ChatMessage target,
        Func<string, Task> onContentChanged,
        CancellationToken cancellationToken)
    {
        if (target.Role != ChatRole.Assistant)
            throw new InvalidOperationException("只能为助手消息生成候选回复。");

        var settings = await LoadConfiguredSettingsAsync();
        var allMessages = (await messageRepository.GetBySessionAsync(session.Id)).ToList();
        var targetPosition = allMessages.FindIndex(x => x.Id == target.Id);
        if (targetPosition < 0) throw new InvalidOperationException("消息不属于当前聊天。");

        var swipes = await EnsureSwipeHistoryAsync(target);
        var originalContent = target.Content;
        var newContent = string.Empty;
        var request = await CreateRequestAsync(
            session, allMessages.Take(targetPosition), settings, target.SpeakerCharacterId);
        await onContentChanged(string.Empty);
        try
        {
            await foreach (var chunk in provider.StreamAsync(request, cancellationToken))
            {
                newContent += chunk;
                await onContentChanged(newContent);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Alternative generation cancelled by user.");
        }
        finally
        {
            if (!string.IsNullOrEmpty(newContent))
            {
                var index = swipes.Count;
                await swipeRepository.AddAsync(new MessageSwipe
                {
                    ChatMessageId = target.Id, SwipeIndex = index,
                    Content = newContent, CreatedAt = DateTimeOffset.UtcNow
                });
                target.Content = newContent;
                target.CurrentSwipeIndex = index;
                target.UpdatedAt = DateTimeOffset.UtcNow;
                await messageRepository.UpdateAsync(target);
                session.UpdatedAt = target.UpdatedAt.Value;
                await sessionRepository.UpdateAsync(session);
            }
            else
            {
                target.Content = originalContent;
                await onContentChanged(originalContent);
            }
        }
        return (target, swipes.Count + (string.IsNullOrEmpty(newContent) ? 0 : 1));
    }

    public static IReadOnlyList<ChatCompletionMessage> BuildMessages(
        IEnumerable<ChatMessage> messages,
        IReadOnlyDictionary<long, string>? speakerNames = null) =>
        messages.Select(message => new ChatCompletionMessage
        {
            Role = message.Role.ToString().ToLowerInvariant(),
            Content = message.Role == ChatRole.Assistant &&
                      message.SpeakerCharacterId is long speakerId &&
                      speakerNames?.TryGetValue(speakerId, out var speakerName) == true
                ? $"[{speakerName}]\n{message.Content}"
                : message.Content,
            SourceMessageId = message.Id
        }).ToList();

    private async Task<ProviderSettings> LoadConfiguredSettingsAsync()
    {
        var settings = await settingsService.LoadAsync();
        if (!settings.IsConfigured)
            throw new InvalidOperationException("尚未配置模型 Provider，请先前往 Settings。");
        return settings;
    }

    private async Task<ChatCompletionRequest> CreateRequestAsync(
        ChatSession session,
        IEnumerable<ChatMessage> history,
        ProviderSettings settings,
        long? speakingCharacterId = null)
    {
        var historyList = history.ToList();
        Character? characterContext = null;
        if (session.CharacterId is long characterId)
            characterContext = await characterRepository.GetAsync(characterId);

        IEnumerable<ChatMessage> requestHistory = historyList;
        if (characterContext is not null && !settings.IncludeCharacterContext &&
            historyList.FirstOrDefault() is { Role: ChatRole.Assistant } greeting &&
            greeting.Content == RenderCharacterText(characterContext.FirstMessage, characterContext.Name))
            requestHistory = historyList.Skip(1);

        var prompt = await promptService.BuildAsync(
            session, requestHistory, settings, speakingCharacterId);
        var messages = new List<ChatCompletionMessage>();
        foreach (var message in prompt.Messages)
        {
            var images = settings.IncludeImageContext && message.SourceMessageId is long messageId
                ? await AttachmentService.ToDataUrlsAsync(await attachmentRepository.GetByMessageAsync(messageId))
                : [];
            messages.Add(new ChatCompletionMessage
            {
                Role = message.Role, Content = message.Content, SourceMessageId = message.SourceMessageId,
                ImageDataUrls = images
            });
        }
        return new ChatCompletionRequest
        {
            Model = prompt.Model, Messages = messages,
            Temperature = prompt.Temperature, TopP = prompt.TopP,
            MaxTokens = prompt.MaxTokens, Stream = true
        };
    }

    private async Task<Character?> SelectSpeakerAsync(
        ChatSession session,
        IReadOnlyList<ChatMessage> history)
    {
        if (!session.IsGroupChat)
            return session.CharacterId is long characterId
                ? await characterRepository.GetAsync(characterId)
                : null;

        var members = (await GetGroupMembersAsync(session.Id)).ToList();
        if (members.Count == 0) return null;
        var latestUserText = history.LastOrDefault(x => x.Role == ChatRole.User)?.Content ?? string.Empty;
        var lastSpeakerId = history.LastOrDefault(x => x.Role == ChatRole.Assistant)?.SpeakerCharacterId;
        var weighted = members.Select(member =>
        {
            var score = 1.0;
            if (latestUserText.Contains(member.Name, StringComparison.OrdinalIgnoreCase)) score += 12;
            var keywords = member.Tags.Split([',', '，', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            score += keywords.Count(x => x.Length >= 2 && latestUserText.Contains(x, StringComparison.OrdinalIgnoreCase)) * 2;
            if (lastSpeakerId == member.Id && members.Count > 1) score *= 0.25;
            return (Member: member, Score: score);
        }).ToList();
        var pick = Random.Shared.NextDouble() * weighted.Sum(x => x.Score);
        foreach (var candidate in weighted)
        {
            pick -= candidate.Score;
            if (pick <= 0) return candidate.Member;
        }
        return weighted[^1].Member;
    }

    private async Task<IReadOnlyList<MessageSwipe>> EnsureSwipeHistoryAsync(ChatMessage message)
    {
        var swipes = await swipeRepository.GetByMessageAsync(message.Id);
        if (swipes.Count > 0 || string.IsNullOrEmpty(message.Content)) return swipes;
        await swipeRepository.AddAsync(new MessageSwipe
        {
            ChatMessageId = message.Id, SwipeIndex = 0,
            Content = message.Content, CreatedAt = message.CreatedAt
        });
        return await swipeRepository.GetByMessageAsync(message.Id);
    }

    private static string RenderCharacterText(string text, string characterName) =>
        text.Replace("{{char}}", characterName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{user}}", "User", StringComparison.OrdinalIgnoreCase);
}
