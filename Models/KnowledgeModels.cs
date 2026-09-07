namespace NativeTavern.Models;

public sealed class KnowledgeDocument
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string ManagedPath { get; set; } = string.Empty;
    public string MimeType { get; set; } = "text/plain";
    public bool IsEnabled { get; set; } = true;
    public int ChunkCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class KnowledgeChunk
{
    public long Id { get; set; }
    public long DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
}

public sealed class ChatAttachment
{
    public long Id { get; set; }
    public long ChatMessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
