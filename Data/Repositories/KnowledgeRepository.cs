using Dapper;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

public sealed class KnowledgeRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<KnowledgeDocument>> GetDocumentsAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<DocumentRow>("SELECT * FROM KnowledgeDocuments ORDER BY CreatedAt DESC");
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task<long> AddAsync(KnowledgeDocument document, IReadOnlyList<KnowledgeChunk> chunks)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        document.Id = await connection.ExecuteScalarAsync<long>(
            "INSERT INTO KnowledgeDocuments(Name,SourcePath,ManagedPath,MimeType,IsEnabled,ChunkCount,CreatedAt) " +
            "VALUES(@Name,@SourcePath,@ManagedPath,@MimeType,@IsEnabled,@ChunkCount,@CreatedAt); SELECT last_insert_rowid();",
            new { document.Name, document.SourcePath, document.ManagedPath, document.MimeType,
                IsEnabled = document.IsEnabled ? 1 : 0, ChunkCount = chunks.Count, CreatedAt = document.CreatedAt.ToString("O") }, transaction);
        foreach (var chunk in chunks)
            await connection.ExecuteAsync("INSERT INTO KnowledgeChunks(DocumentId,ChunkIndex,Content) VALUES(@DocumentId,@ChunkIndex,@Content)",
                new { DocumentId = document.Id, chunk.ChunkIndex, chunk.Content }, transaction);
        await transaction.CommitAsync();
        return document.Id;
    }

    public async Task SetEnabledAsync(long id, bool isEnabled)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE KnowledgeDocuments SET IsEnabled=@isEnabled WHERE Id=@id", new { id, isEnabled = isEnabled ? 1 : 0 });
    }

    public async Task DeleteAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM KnowledgeDocuments WHERE Id=@id", new { id });
    }

    public async Task<IReadOnlyList<KnowledgeChunk>> GetEnabledChunksAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ChunkRow>(
            "SELECT c.* FROM KnowledgeChunks c INNER JOIN KnowledgeDocuments d ON d.Id=c.DocumentId WHERE d.IsEnabled=1");
        return rows.Select(x => new KnowledgeChunk { Id=x.Id, DocumentId=x.DocumentId, ChunkIndex=x.ChunkIndex, Content=x.Content }).ToList();
    }

    private sealed class DocumentRow
    {
        public long Id { get; init; } public string Name { get; init; } = ""; public string SourcePath { get; init; } = ""; public string ManagedPath { get; init; } = ""; public string MimeType { get; init; } = ""; public int IsEnabled { get; init; } public int ChunkCount { get; init; } public string CreatedAt { get; init; } = "";
        public KnowledgeDocument ToModel() => new() { Id=Id, Name=Name, SourcePath=SourcePath, ManagedPath=ManagedPath, MimeType=MimeType, IsEnabled=IsEnabled != 0, ChunkCount=ChunkCount, CreatedAt=DateTimeOffset.Parse(CreatedAt) };
    }
    private sealed class ChunkRow { public long Id { get; init; } public long DocumentId { get; init; } public int ChunkIndex { get; init; } public string Content { get; init; } = ""; }
}
