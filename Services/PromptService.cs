using NativeTavern.Data.Repositories;
using NativeTavern.Models;

namespace NativeTavern.Services;

public sealed class PromptService(PromptRepository repository, CharacterRepository characterRepository)
{
    public async Task<PromptBuildResult> BuildAsync(
        ChatSession session,
        IEnumerable<ChatMessage> history,
        ProviderSettings settings)
    {
        var historyList = history.ToList();
        var sections = new List<string>();
        PromptPreset? preset = null;
        if (session.PromptPresetId is long presetId)
            preset = (await repository.GetPresetsAsync()).FirstOrDefault(x => x.Id == presetId);
        AddSection(sections, "System Prompt", preset?.SystemPrompt);
        AddSection(sections, "Main Prompt", preset?.MainPrompt);

        if (settings.IncludeCharacterContext && session.CharacterId is long characterId &&
            await characterRepository.GetAsync(characterId) is { } character)
            AddSection(sections, "Character", BuildCharacterContext(character));

        if (session.PersonaId is long personaId &&
            (await repository.GetPersonasAsync()).FirstOrDefault(x => x.Id == personaId) is { } persona)
            AddSection(sections, "Persona", persona.Content);

        var activatedNames = new List<string>();
        if (session.LorebookId is long lorebookId &&
            (await repository.GetLorebooksAsync()).FirstOrDefault(x => x.Id == lorebookId) is { IsEnabled: true })
        {
            var activated = ActivateLoreEntries(await repository.GetLoreEntriesAsync(lorebookId), historyList);
            foreach (var entry in activated)
            {
                AddSection(sections, "World Info: " + entry.Name, entry.Content);
                activatedNames.Add(entry.Name);
            }
        }

        var messages = ChatService.BuildMessages(historyList).ToList();
        if (sections.Count > 0)
            messages.Insert(0, new ChatCompletionMessage { Role = "system", Content = string.Join("\n\n", sections) });
        if (!string.IsNullOrWhiteSpace(session.AuthorNote))
        {
            var position = Math.Max(messages.Count - 1, sections.Count > 0 ? 1 : 0);
            messages.Insert(position, new ChatCompletionMessage
            {
                Role = "system",
                Content = "Author Note:\n" + session.AuthorNote.Trim()
            });
        }

        return new PromptBuildResult
        {
            Messages = messages,
            Model = string.IsNullOrWhiteSpace(preset?.Model) ? settings.Model : preset.Model.Trim(),
            Temperature = preset?.Temperature ?? settings.Temperature,
            TopP = preset?.TopP ?? settings.TopP,
            MaxTokens = preset?.MaxTokens ?? settings.MaxTokens,
            ActivatedLoreEntries = activatedNames
        };
    }

    public static IReadOnlyList<LoreEntry> ActivateLoreEntries(
        IEnumerable<LoreEntry> entries,
        IReadOnlyList<ChatMessage> history)
    {
        return entries.Where(entry =>
        {
            if (!entry.IsEnabled) return false;
            var depth = Math.Max(1, entry.Depth);
            var text = string.Join("\n", history.TakeLast(depth).Select(x => x.Content));
            if (!entry.IsConstant && !ContainsAny(text, SplitKeywords(entry.Keywords))) return false;
            return !entry.IsSelective || ContainsAny(text, SplitKeywords(entry.SecondaryKeywords));
        }).OrderByDescending(x => x.Priority).ThenBy(x => x.Id).ToList();
    }

    private static IEnumerable<string> SplitKeywords(string value) =>
        value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool ContainsAny(string text, IEnumerable<string> keywords) =>
        keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static void AddSection(ICollection<string> sections, string heading, string? content)
    {
        if (!string.IsNullOrWhiteSpace(content)) sections.Add(heading + ":\n" + content.Trim());
    }

    private static string BuildCharacterContext(Character character)
    {
        var sections = new[]
        {
            $"You are {character.Name}. Stay in character throughout the conversation.",
            string.IsNullOrWhiteSpace(character.Description) ? null : "Description:\n" + character.Description,
            string.IsNullOrWhiteSpace(character.Personality) ? null : "Personality:\n" + character.Personality,
            string.IsNullOrWhiteSpace(character.Scenario) ? null : "Scenario:\n" + character.Scenario,
            string.IsNullOrWhiteSpace(character.ExampleMessages) ? null : "Example dialogue:\n" + character.ExampleMessages
        };
        return RenderCharacterText(string.Join("\n\n", sections.Where(x => x is not null)), character.Name);
    }

    private static string RenderCharacterText(string text, string characterName) =>
        text.Replace("{{char}}", characterName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{user}}", "User", StringComparison.OrdinalIgnoreCase);
}
