using Dapper;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

public sealed class PromptRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<Persona>> GetPersonasAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PersonaRow>("SELECT * FROM Personas ORDER BY UpdatedAt DESC");
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task SavePersonaAsync(Persona value)
    {
        var now = DateTimeOffset.UtcNow;
        value.UpdatedAt = now;
        await using var connection = connectionFactory.CreateConnection();
        if (value.Id == 0)
        {
            value.CreatedAt = now;
            value.Id = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO Personas(Name,Content,CreatedAt,UpdatedAt) VALUES(@Name,@Content,@CreatedAt,@UpdatedAt); SELECT last_insert_rowid();",
                new { value.Name, value.Content, CreatedAt = now.ToString("O"), UpdatedAt = now.ToString("O") });
        }
        else
            await connection.ExecuteAsync("UPDATE Personas SET Name=@Name,Content=@Content,UpdatedAt=@UpdatedAt WHERE Id=@Id",
                new { value.Id, value.Name, value.Content, UpdatedAt = now.ToString("O") });
    }

    public async Task DeletePersonaAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE ChatSessions SET PersonaId=NULL WHERE PersonaId=@id; DELETE FROM Personas WHERE Id=@id", new { id });
    }

    public async Task<IReadOnlyList<Lorebook>> GetLorebooksAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<LorebookRow>("SELECT * FROM Lorebooks ORDER BY UpdatedAt DESC");
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task SaveLorebookAsync(Lorebook value)
    {
        var now = DateTimeOffset.UtcNow;
        value.UpdatedAt = now;
        await using var connection = connectionFactory.CreateConnection();
        if (value.Id == 0)
        {
            value.CreatedAt = now;
            value.Id = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO Lorebooks(Name,Description,IsEnabled,CreatedAt,UpdatedAt) VALUES(@Name,@Description,@IsEnabled,@CreatedAt,@UpdatedAt); SELECT last_insert_rowid();",
                new { value.Name, value.Description, IsEnabled = value.IsEnabled ? 1 : 0, CreatedAt = now.ToString("O"), UpdatedAt = now.ToString("O") });
        }
        else
            await connection.ExecuteAsync("UPDATE Lorebooks SET Name=@Name,Description=@Description,IsEnabled=@IsEnabled,UpdatedAt=@UpdatedAt WHERE Id=@Id",
                new { value.Id, value.Name, value.Description, IsEnabled = value.IsEnabled ? 1 : 0, UpdatedAt = now.ToString("O") });
    }

    public async Task DeleteLorebookAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE ChatSessions SET LorebookId=NULL WHERE LorebookId=@id; DELETE FROM Lorebooks WHERE Id=@id", new { id });
    }

    public async Task<IReadOnlyList<LoreEntry>> GetLoreEntriesAsync(long lorebookId)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<LoreEntryRow>(
            "SELECT * FROM LoreEntries WHERE LorebookId=@lorebookId ORDER BY Priority DESC,Id", new { lorebookId });
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task SaveLoreEntryAsync(LoreEntry value)
    {
        await using var connection = connectionFactory.CreateConnection();
        var data = new
        {
            value.Id, value.LorebookId, value.Name, value.Keywords, value.SecondaryKeywords,
            value.Content, value.Priority, Depth = Math.Max(1, value.Depth),
            IsEnabled = value.IsEnabled ? 1 : 0, IsConstant = value.IsConstant ? 1 : 0,
            IsSelective = value.IsSelective ? 1 : 0
        };
        if (value.Id == 0)
            value.Id = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO LoreEntries(LorebookId,Name,Keywords,SecondaryKeywords,Content,Priority,Depth,IsEnabled,IsConstant,IsSelective) " +
                "VALUES(@LorebookId,@Name,@Keywords,@SecondaryKeywords,@Content,@Priority,@Depth,@IsEnabled,@IsConstant,@IsSelective); SELECT last_insert_rowid();", data);
        else
            await connection.ExecuteAsync(
                "UPDATE LoreEntries SET Name=@Name,Keywords=@Keywords,SecondaryKeywords=@SecondaryKeywords,Content=@Content,Priority=@Priority," +
                "Depth=@Depth,IsEnabled=@IsEnabled,IsConstant=@IsConstant,IsSelective=@IsSelective WHERE Id=@Id", data);
    }

    public async Task DeleteLoreEntryAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM LoreEntries WHERE Id=@id", new { id });
    }

    public async Task<IReadOnlyList<PromptPreset>> GetPresetsAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PresetRow>("SELECT * FROM PromptPresets ORDER BY UpdatedAt DESC");
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task SavePresetAsync(PromptPreset value)
    {
        var now = DateTimeOffset.UtcNow;
        value.UpdatedAt = now;
        await using var connection = connectionFactory.CreateConnection();
        var data = new { value.Id, value.Name, value.SystemPrompt, value.MainPrompt, value.Model, value.Temperature, value.TopP, value.MaxTokens, UpdatedAt = now.ToString("O"), CreatedAt = (value.CreatedAt == default ? now : value.CreatedAt).ToString("O") };
        if (value.Id == 0)
        {
            value.CreatedAt = now;
            value.Id = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO PromptPresets(Name,SystemPrompt,MainPrompt,Model,Temperature,TopP,MaxTokens,CreatedAt,UpdatedAt) " +
                "VALUES(@Name,@SystemPrompt,@MainPrompt,@Model,@Temperature,@TopP,@MaxTokens,@CreatedAt,@UpdatedAt); SELECT last_insert_rowid();", data);
        }
        else
            await connection.ExecuteAsync(
                "UPDATE PromptPresets SET Name=@Name,SystemPrompt=@SystemPrompt,MainPrompt=@MainPrompt,Model=@Model,Temperature=@Temperature,TopP=@TopP,MaxTokens=@MaxTokens,UpdatedAt=@UpdatedAt WHERE Id=@Id", data);
    }

    public async Task DeletePresetAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE ChatSessions SET PromptPresetId=NULL WHERE PromptPresetId=@id; DELETE FROM PromptPresets WHERE Id=@id", new { id });
    }

    private sealed class PersonaRow
    {
        public long Id { get; init; } public string Name { get; init; } = ""; public string Content { get; init; } = "";
        public string CreatedAt { get; init; } = ""; public string UpdatedAt { get; init; } = "";
        public Persona ToModel() => new() { Id=Id, Name=Name, Content=Content, CreatedAt=DateTimeOffset.Parse(CreatedAt), UpdatedAt=DateTimeOffset.Parse(UpdatedAt) };
    }
    private sealed class LorebookRow
    {
        public long Id { get; init; } public string Name { get; init; } = ""; public string Description { get; init; } = ""; public int IsEnabled { get; init; }
        public string CreatedAt { get; init; } = ""; public string UpdatedAt { get; init; } = "";
        public Lorebook ToModel() => new() { Id=Id, Name=Name, Description=Description, IsEnabled=IsEnabled != 0, CreatedAt=DateTimeOffset.Parse(CreatedAt), UpdatedAt=DateTimeOffset.Parse(UpdatedAt) };
    }
    private sealed class LoreEntryRow
    {
        public long Id { get; init; } public long LorebookId { get; init; } public string Name { get; init; } = ""; public string Keywords { get; init; } = ""; public string SecondaryKeywords { get; init; } = ""; public string Content { get; init; } = "";
        public int Priority { get; init; } public int Depth { get; init; } public int IsEnabled { get; init; } public int IsConstant { get; init; } public int IsSelective { get; init; }
        public LoreEntry ToModel() => new() { Id=Id, LorebookId=LorebookId, Name=Name, Keywords=Keywords, SecondaryKeywords=SecondaryKeywords, Content=Content, Priority=Priority, Depth=Depth, IsEnabled=IsEnabled!=0, IsConstant=IsConstant!=0, IsSelective=IsSelective!=0 };
    }
    private sealed class PresetRow
    {
        public long Id { get; init; } public string Name { get; init; } = ""; public string SystemPrompt { get; init; } = ""; public string MainPrompt { get; init; } = ""; public string Model { get; init; } = "";
        public double? Temperature { get; init; } public double? TopP { get; init; } public int? MaxTokens { get; init; } public string CreatedAt { get; init; } = ""; public string UpdatedAt { get; init; } = "";
        public PromptPreset ToModel() => new() { Id=Id, Name=Name, SystemPrompt=SystemPrompt, MainPrompt=MainPrompt, Model=Model, Temperature=Temperature, TopP=TopP, MaxTokens=MaxTokens, CreatedAt=DateTimeOffset.Parse(CreatedAt), UpdatedAt=DateTimeOffset.Parse(UpdatedAt) };
    }
}
