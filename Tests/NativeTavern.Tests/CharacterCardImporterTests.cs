using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using NativeTavern.Importers;
using NativeTavern.Models;
using NativeTavern.Providers;
using NativeTavern.Services;
using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

public sealed class CharacterCardImporterTests
{
    private readonly CharacterCardImporter _importer = new();

    [Fact]
    public void ParsesV2Json()
    {
        const string json = """
        {
          "spec": "chara_card_v2",
          "spec_version": "2.0",
          "data": {
            "name": "Alice",
            "description": "Explorer",
            "personality": "Curious",
            "scenario": "A tavern",
            "first_mes": "Hello {{user}}",
            "mes_example": "{{char}}: Welcome",
            "creator": "Tester",
            "tags": ["adventure", "friendly"]
          }
        }
        """;
        var result = _importer.ParseJson(json);
        Assert.Equal("Alice", result.Name);
        Assert.Equal("Hello {{user}}", result.FirstMessage);
        Assert.Equal("adventure, friendly", result.Tags);
    }

    [Fact]
    public void ParsesV3Json()
    {
        const string json = """
        {"spec":"chara_card_v3","spec_version":"3.0","data":{"name":"Vera","description":"V3 character","first_mes":"Greetings"}}
        """;
        Assert.Equal("Vera", _importer.ParseJson(json).Name);
    }

    [Fact]
    public void ExportedCharacterCardRoundTripsThroughV3Importer()
    {
        var source = new Character
        {
            Name = "Vera",
            Description = "Explorer",
            Personality = "Curious",
            Scenario = "A tavern",
            FirstMessage = "Hello {{user}}",
            ExampleMessages = "{{char}}: Welcome",
            Creator = "Tester",
            Tags = "adventure, friendly"
        };

        var json = DataExportService.BuildCharacterCardJson(source);
        var exported = _importer.ParseJson(json);

        Assert.Contains("\"spec\":\"chara_card_v3\"", json);
        Assert.Equal(source.Name, exported.Name);
        Assert.Equal(source.FirstMessage, exported.FirstMessage);
        Assert.Equal(source.Tags, exported.Tags);
    }

    [Fact]
    public void ConversationExportPreservesGroupSpeakerNames()
    {
        var session = new ChatSession
        {
            Title = "Adventure",
            IsGroupChat = true,
            CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            UpdatedAt = DateTimeOffset.Parse("2026-01-01T00:01:00Z")
        };
        var messages = new[]
        {
            new ChatMessage
            {
                Role = ChatRole.User,
                Content = "Hello",
                CreatedAt = session.CreatedAt
            },
            new ChatMessage
            {
                Role = ChatRole.Assistant,
                SpeakerCharacterId = 7,
                Content = "Welcome",
                CreatedAt = session.UpdatedAt
            }
        };

        var markdown = DataExportService.BuildConversationMarkdown(
            session, messages, new Dictionary<long, string> { [7] = "Alice" });
        var json = DataExportService.BuildConversationJson(
            session, messages, new Dictionary<long, string> { [7] = "Alice" });

        Assert.Contains("# Adventure", markdown);
        Assert.Contains("## Alice", markdown);
        Assert.Contains("Welcome", markdown);
        Assert.Contains("\"speaker\":\"Alice\"", json);
        Assert.Contains("\"is_group_chat\":true", json);
    }

    [Fact]
    public async Task CharacterExportAtomicallyReplacesExistingFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NativeTavernExportTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "character.json");
        try
        {
            await File.WriteAllTextAsync(path, "old content");

            await DataExportService.ExportCharacterAsync(new Character { Name = "Alice" }, path);

            Assert.Equal("Alice", _importer.ParseJson(await File.ReadAllTextAsync(path)).Name);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task PngPrefersCcv3OverChara()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            var v2 = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"data":{"name":"V2"}}"""));
            var v3 = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"data":{"name":"V3"}}"""));
            await File.WriteAllBytesAsync(path, BuildPng(("chara", v2), ("ccv3", v3)));
            Assert.Equal("V3", (await _importer.ImportAsync(path)).Name);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void MapsChatHistoryToProviderMessages()
    {
        var messages = new[]
        {
            new ChatMessage { Role = ChatRole.User, Content = "Hi" },
            new ChatMessage { Role = ChatRole.Assistant, Content = "Hello" }
        };
        var result = ChatService.BuildMessages(messages);
        Assert.Collection(result,
            first => Assert.Equal("user", first.Role),
            second => Assert.Equal("assistant", second.Role));
    }

    [Fact]
    public void GroupChatHistoryIncludesAssistantSpeakerName()
    {
        var messages = new[]
        {
            new ChatMessage
            {
                Role = ChatRole.Assistant,
                SpeakerCharacterId = 7,
                Content = "Welcome"
            }
        };

        var result = ChatService.BuildMessages(messages, new Dictionary<long, string> { [7] = "Alice" });

        Assert.Equal("[Alice]\nWelcome", Assert.Single(result).Content);
    }

    [Fact]
    public void MessageViewModelTracksEditAndSwipeState()
    {
        var model = new ChatMessage
        {
            Role = ChatRole.Assistant,
            Content = "First",
            CurrentSwipeIndex = 0
        };
        var viewModel = new ChatMessageViewModel(model, 2);

        viewModel.SetSwipeState(1, 2, "Second");
        Assert.Equal("Second", viewModel.Content);
        Assert.Equal("2 / 2", viewModel.SwipeDisplay);
        Assert.True(viewModel.CanSwipeLeft);

        viewModel.BeginEdit();
        viewModel.EditText = "Edited";
        viewModel.FinishEdit();
        Assert.Equal("Edited", model.Content);
        Assert.False(viewModel.IsEditing);
    }

    [Fact]
    public void LorebookActivationHonorsDepthSelectivityAndPriority()
    {
        var history = new[]
        {
            new ChatMessage { Content = "The old forest contained a hidden gate." },
            new ChatMessage { Content = "We are talking about the moon now." }
        };
        var entries = new[]
        {
            new LoreEntry { Id = 1, Name = "Too shallow", Keywords = "forest", Content = "A", Depth = 1, IsEnabled = true, Priority = 300 },
            new LoreEntry { Id = 2, Name = "Selective", Keywords = "moon", SecondaryKeywords = "talking", Content = "B", Depth = 2, IsEnabled = true, IsSelective = true, Priority = 200 },
            new LoreEntry { Id = 3, Name = "Constant", Content = "C", Depth = 1, IsEnabled = true, IsConstant = true, Priority = 100 }
        };

        var result = PromptService.ActivateLoreEntries(entries, history);

        Assert.Equal(["Selective", "Constant"], result.Select(x => x.Name));
    }

    [Fact]
    public void DisabledLoreEntriesNeverActivate()
    {
        var entry = new LoreEntry { Name = "Disabled", Content = "Hidden", IsEnabled = false, IsConstant = true };
        Assert.Empty(PromptService.ActivateLoreEntries([entry], []));
    }

    [Fact]
    public void ProviderProfilesCoverV05Targets()
    {
        var ids = ProviderProfile.All.Select(x => x.Id).ToHashSet();
        Assert.True(new[] { "ollama", "lmstudio", "openrouter", "claude", "gemini" }.All(ids.Contains));
    }

    [Fact]
    public void ParsesOpenAiCompatibleModelsAndStreamChunks()
    {
        var models = OpenAICompatibleProvider.ParseModels(
            """{"data":[{"id":"z-model"},{"id":"a-model","name":"Alpha"}]}""");
        Assert.Equal(["a-model", "z-model"], models.Select(x => x.Id));
        Assert.Equal("Hello", OpenAICompatibleProvider.ParseStreamingContent(
            """{"choices":[{"delta":{"content":"Hello"}}]}"""));
    }

    [Fact]
    public void ParsesClaudeModelsAndTextDelta()
    {
        var models = ClaudeProvider.ParseModels(
            """{"data":[{"id":"claude-test","display_name":"Claude Test"}]}""");
        Assert.Equal("claude-test", Assert.Single(models).Id);
        Assert.Equal("Hi", ClaudeProvider.ParseStreamingContent(
            """{"type":"content_block_delta","delta":{"type":"text_delta","text":"Hi"}}"""));
        Assert.Null(ClaudeProvider.ParseStreamingContent("""{"type":"ping"}"""));
    }

    [Fact]
    public void ClaudeStreamErrorsBecomeProviderErrors()
    {
        var error = Assert.Throws<ProviderException>(() => ClaudeProvider.ParseStreamingContent(
            """{"type":"error","error":{"message":"overloaded"}}"""));
        Assert.Equal("overloaded", error.Message);
    }

    [Fact]
    public void TokenEstimatorAccountsForCjkAndImages()
    {
        var textOnly = TokenEstimator.Estimate([
            new ChatCompletionMessage { Role = "user", Content = "你好 world" }
        ]);
        var withImage = TokenEstimator.Estimate([
            new ChatCompletionMessage { Role = "user", Content = "你好 world", ImageDataUrls = ["data:image/png;base64,AA=="] }
        ]);
        Assert.True(textOnly > 4);
        Assert.Equal(85, withImage - textOnly);
    }

    [Fact]
    public void ImageMessageUsesOpenAiContentParts()
    {
        var json = JsonSerializer.Serialize(new ChatCompletionMessage
        {
            Role = "user", Content = "Look", ImageDataUrls = ["data:image/png;base64,AA=="]
        });
        Assert.Contains("image_url", json);
        Assert.Contains("data:image/png", json);
        Assert.Contains("Look", json);
    }

    private static byte[] BuildPng(params (string Key, string Value)[] cards)
    {
        using var stream = new MemoryStream();
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        foreach (var card in cards)
        {
            var data = Encoding.ASCII.GetBytes(card.Key + (char)0 + card.Value);
            WriteChunk(stream, "tEXt", data);
        }
        WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        stream.Write(length);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write([0, 0, 0, 0]);
    }
}
