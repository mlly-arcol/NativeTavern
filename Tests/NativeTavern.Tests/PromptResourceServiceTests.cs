using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

/// <summary>Covers prompt resource export/import, including foreign SillyTavern world-info books.</summary>
public class PromptResourceServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly DatabaseConnectionFactory _factory;
    private readonly PromptRepository _prompts;
    private readonly RegexScriptRepository _scripts;
    private readonly PromptResourceService _service;

    public PromptResourceServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "NativeTavernPack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _factory = new DatabaseConnectionFactory(Path.Combine(_directory, "data.db"));
        new DatabaseInitializer(_factory, NullLogger<DatabaseInitializer>.Instance)
            .InitializeAsync().GetAwaiter().GetResult();
        _prompts = new PromptRepository(_factory);
        _scripts = new RegexScriptRepository(_factory);
        _service = new PromptResourceService(
            _prompts,
            _scripts,
            new RegexScriptService(_scripts, NullLogger<RegexScriptService>.Instance));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }

    private string TempPath(string name) => Path.Combine(_directory, name);

    private async Task SeedAsync()
    {
        await _prompts.SavePersonaAsync(new Persona { Name = "Writer", Content = "I write poetry." });
        var book = new Lorebook { Name = "Realm", Description = "Places", IsEnabled = false };
        await _prompts.SaveLorebookAsync(book);
        await _prompts.SaveLoreEntryAsync(new LoreEntry
        {
            LorebookId = book.Id, Name = "Capital", Keywords = "city, capital",
            Content = "The capital sits on a river.", Priority = 80, Depth = 2, IsConstant = true,
        });
        await _prompts.SaveLoreEntryAsync(new LoreEntry
        {
            LorebookId = book.Id, Name = "Bridge", Keywords = "bridge", Content = "An old stone bridge.",
            IsEnabled = false,
        });
        await _prompts.SavePresetAsync(new PromptPreset
        {
            Name = "Novel", SystemPrompt = "Be vivid.", MainPrompt = "Write {scene}",
            Model = "gpt-test", Temperature = 0.8, TopP = 0.9, MaxTokens = 1200,
        });
        await _scripts.SaveAsync(new RegexScript
        {
            Name = "Trim asterisks", Pattern = @"\*(\w+)\*", Replacement = "$1",
            Target = RegexScriptTarget.AssistantOutput, Mode = RegexScriptMode.Regex, SortOrder = 1,
        });
    }

    [Fact]
    public async Task ExportWritesEveryResourceKind()
    {
        await SeedAsync();
        var pack = await _service.BuildPackAsync();

        Assert.Equal(PromptResourceService.PackFormat, pack.Format);
        Assert.Single(pack.Personas);
        Assert.Single(pack.Lorebooks);
        Assert.Equal(2, pack.Lorebooks[0].Entries.Count);
        Assert.Single(pack.Presets);
        Assert.Single(pack.RegexScripts);
        Assert.Equal(0.8, pack.Presets[0].Temperature);
        Assert.Equal("Regex", pack.RegexScripts[0].Mode);
    }

    [Fact]
    public async Task ExportThenImportDuplicatesResourcesWithUniqueNames()
    {
        await SeedAsync();
        var path = TempPath("pack.json");
        await _service.ExportAllAsync(path);

        var report = await _service.ImportFileAsync(path);

        Assert.Equal(1, report.Personas);
        Assert.Equal(1, report.Lorebooks);
        Assert.Equal(2, report.LoreEntries);
        Assert.Equal(1, report.Presets);
        Assert.Equal(1, report.Scripts);
        Assert.Empty(report.Warnings);

        Assert.Equal(2, (await _prompts.GetPersonasAsync()).Count);
        var books = await _prompts.GetLorebooksAsync();
        var copy = Assert.Single(books, x => x.Name != "Realm");
        Assert.Equal("Realm (2)", copy.Name);
        // The switch travels with the book, so an imported pack behaves like the original.
        Assert.False(copy.IsEnabled);
        var entries = await _prompts.GetLoreEntriesAsync(copy.Id);
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, x => x.Name == "Capital" && x.IsConstant && x.Depth == 2);
        Assert.Contains(entries, x => x.Name == "Bridge" && !x.IsEnabled);

        var presets = await _prompts.GetPresetsAsync();
        var preset = Assert.Single(presets, x => x.Name != "Novel");
        Assert.Equal("Novel (2)", preset.Name);
        Assert.Equal(1200, preset.MaxTokens);
        Assert.Equal("Write {scene}", preset.MainPrompt);

        var scripts = await _scripts.GetAllAsync();
        Assert.Equal(2, scripts.Count);
        Assert.Contains(scripts, x => x.Name == "Trim asterisks (2)" && x.Pattern == @"\*(\w+)\*");
    }

    [Fact]
    public async Task ImportReadsSillyTavernWorldInfoBook()
    {
        var json = """
        {
          "name": "Outer Realm",
          "entries": {
            "0": { "comment": "Gate", "keys": ["gate", "wall"], "content": "The gate opens at dawn.",
                   "enabled": true, "insertion_order": "12", "constant": 1 },
            "1": { "comment": "Fog", "keys": "fog", "content": "", "enabled": true },
            "2": { "keys": ["nameless"], "content": "Has no comment field." }
          }
        }
        """;
        var path = TempPath("world.json");
        await File.WriteAllTextAsync(path, json);

        var report = await _service.ImportFileAsync(path);

        Assert.Equal(1, report.Lorebooks);
        Assert.Equal(2, report.LoreEntries);
        var book = Assert.Single(await _prompts.GetLorebooksAsync());
        Assert.Equal("Outer Realm", book.Name);
        var entries = await _prompts.GetLoreEntriesAsync(book.Id);
        Assert.Equal(2, entries.Count);
        var gate = Assert.Single(entries, x => x.Name == "Gate");
        Assert.Equal("gate, wall", gate.Keywords);
        Assert.Equal(12, gate.Priority);
        Assert.True(gate.IsConstant);
        Assert.Equal(4, gate.Depth);
        Assert.Contains(entries, x => x.Name == "Entry 2");
    }

    [Fact]
    public async Task ImportReadsWorldInfoEmbeddedInACharacterCard()
    {
        var json = """
        {
          "spec": "chara_card_v3",
          "data": {
            "name": "Moon Cultivator",
            "character_book": {
              "name": "Moon Cultivator",
              "entries": [ { "comment": "Sect", "keys": ["sect"], "content": "A quiet mountain sect." } ]
            }
          }
        }
        """;
        var path = TempPath("card.json");
        await File.WriteAllTextAsync(path, json);

        var report = await _service.ImportFileAsync(path);

        Assert.Equal(1, report.Lorebooks);
        var book = Assert.Single(await _prompts.GetLorebooksAsync());
        Assert.Equal("Moon Cultivator", book.Name);
        Assert.Equal("A quiet mountain sect.", (await _prompts.GetLoreEntriesAsync(book.Id))[0].Content);
    }

    [Fact]
    public async Task ImportSkipsScriptsThatDoNotCompile()
    {
        var json = """
        {
          "format": "nativetavern_prompt_pack",
          "version": 1,
          "exportedAt": "2026-01-01T00:00:00+00:00",
          "personas": [ { "name": "", "content": "orphan" }, { "name": "Sage", "content": "Calm voice." } ],
          "presets": [ { "name": "Plain" } ],
          "regexScripts": [ { "name": "Broken", "pattern": "(", "mode": "Regex" } ]
        }
        """;
        var report = await _service.ImportAsync(JsonDocument.Parse(json).RootElement.Clone());

        Assert.Equal(1, report.Personas);
        Assert.Equal(1, report.Presets);
        Assert.Equal(0, report.Scripts);
        Assert.Equal(2, report.Warnings.Count);
        Assert.Single(await _prompts.GetPersonasAsync());
        Assert.Empty(await _scripts.GetAllAsync());
        // A preset with no prompt fields still imports so the user can edit it later.
        Assert.Equal(string.Empty, (await _prompts.GetPresetsAsync())[0].SystemPrompt);
    }

    [Fact]
    public async Task UnknownFormatsAndWrongExtensionAreRejected()
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await _service.ImportAsync(JsonDocument.Parse("""{"a":1}""").RootElement.Clone()));
        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await _service.ExportAllAsync(TempPath("pack.txt")));
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _service.ImportFileAsync(TempPath("missing.json")));
    }
}
