using System.Text;
using System.Text.Json;
using NativeTavern.Helpers;
using NativeTavern.Models;

namespace NativeTavern.Services;

public static class DataExportService
{
    public static async Task ExportCharacterAsync(
        Character character,
        string path,
        CancellationToken cancellationToken = default)
    {
        EnsureExtension(path, ".json");
        await WriteAllTextAtomicAsync(path, BuildCharacterCardJson(character), cancellationToken);
    }

    public static string BuildCharacterCardJson(Character character)
    {
        if (string.IsNullOrWhiteSpace(character.Name))
            throw new InvalidOperationException("角色名称不能为空。");

        var card = new
        {
            spec = "chara_card_v3",
            spec_version = "3.0",
            data = new
            {
                name = character.Name,
                description = character.Description,
                personality = character.Personality,
                scenario = character.Scenario,
                first_mes = character.FirstMessage,
                mes_example = character.ExampleMessages,
                creator = character.Creator,
                tags = SplitTags(character.Tags),
                alternate_greetings = Array.Empty<string>(),
                extensions = new { }
            }
        };
        return JsonSerializer.Serialize(card, JsonDefaults.Options);
    }

    public static async Task ExportConversationAsync(
        ChatSession session,
        IEnumerable<ChatMessage> messages,
        IReadOnlyDictionary<long, string>? speakerNames,
        string path,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var content = extension switch
        {
            ".md" or ".markdown" => BuildConversationMarkdown(session, messages, speakerNames),
            ".json" => BuildConversationJson(session, messages, speakerNames),
            _ => throw new InvalidDataException("聊天记录仅支持导出为 Markdown 或 JSON。")
        };
        await WriteAllTextAtomicAsync(path, content, cancellationToken);
    }

    public static string BuildConversationMarkdown(
        ChatSession session,
        IEnumerable<ChatMessage> messages,
        IReadOnlyDictionary<long, string>? speakerNames = null)
    {
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(session.Title.Trim());
        builder.AppendLine();
        builder.Append("> NativeTavern conversation exported ")
            .AppendLine(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        if (session.ParentSessionId is not null)
            builder.AppendLine("> This conversation was created from a branch.");

        foreach (var message in messages)
        {
            builder.AppendLine();
            builder.Append("## ").Append(GetSpeakerLabel(message, speakerNames))
                .Append(" · ").AppendLine(message.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            builder.AppendLine();
            builder.AppendLine(message.Content);
        }
        return builder.ToString();
    }

    public static string BuildConversationJson(
        ChatSession session,
        IEnumerable<ChatMessage> messages,
        IReadOnlyDictionary<long, string>? speakerNames = null)
    {
        var export = new
        {
            format = "nativetavern_conversation",
            version = 1,
            conversation = new
            {
                title = session.Title,
                is_group_chat = session.IsGroupChat,
                parent_session_id = session.ParentSessionId,
                branched_from_message_id = session.BranchedFromMessageId,
                created_at = session.CreatedAt,
                updated_at = session.UpdatedAt
            },
            messages = messages.Select(message => new
            {
                role = message.Role.ToString().ToLowerInvariant(),
                speaker = GetSpeakerLabel(message, speakerNames),
                content = message.Content,
                created_at = message.CreatedAt,
                updated_at = message.UpdatedAt
            }).ToArray()
        };
        return JsonSerializer.Serialize(export, JsonDefaults.Options);
    }

    public static string CreateSafeFileName(string value, string fallback)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var result = new string(value.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(result) ? fallback : result;
    }

    private static string GetSpeakerLabel(
        ChatMessage message,
        IReadOnlyDictionary<long, string>? speakerNames)
    {
        if (message.Role == ChatRole.User) return "User";
        return message.SpeakerCharacterId is long id &&
               speakerNames?.TryGetValue(id, out var name) == true &&
               !string.IsNullOrWhiteSpace(name)
            ? name
            : "Assistant";
    }

    private static string[] SplitTags(string tags) =>
        tags.Split([',', '，', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void EnsureExtension(string path, string expected)
    {
        if (!Path.GetExtension(path).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"文件必须使用 {expected} 扩展名。");
    }

    private static async Task WriteAllTextAtomicAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
                        ?? throw new InvalidDataException("导出路径无效。");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken);
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
