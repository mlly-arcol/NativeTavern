using System.Text;
using Microsoft.Extensions.Logging;
using NativeTavern.Data.Repositories;
using NativeTavern.Helpers;
using NativeTavern.Models;
using UglyToad.PdfPig;

namespace NativeTavern.Services;

public sealed class KnowledgeService(
    KnowledgeRepository repository,
    ILogger<KnowledgeService> logger)
{
    private const long MaxFileSize = 20 * 1024 * 1024;

    public Task<IReadOnlyList<KnowledgeDocument>> GetDocumentsAsync() => repository.GetDocumentsAsync();

    public async Task<KnowledgeDocument> ImportAsync(string sourcePath)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("找不到文档。", sourcePath);
        var info = new FileInfo(sourcePath);
        if (info.Length > MaxFileSize) throw new InvalidDataException("文档不能超过 20 MB。");
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".txt" or ".md" or ".markdown" or ".pdf"))
            throw new InvalidDataException("知识库仅支持 TXT、Markdown 和 PDF。");

        var text = await ExtractTextAsync(sourcePath, extension);
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("未能从文档提取可检索文本。");
        var destination = Path.Combine(AppPaths.DocumentsDirectory, $"{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePath, destination, false);
        var chunks = Chunk(text).Select((content, index) => new KnowledgeChunk { ChunkIndex = index, Content = content }).ToList();
        var document = new KnowledgeDocument
        {
            Name = Path.GetFileName(sourcePath), SourcePath = sourcePath, ManagedPath = destination,
            MimeType = extension == ".pdf" ? "application/pdf" : "text/plain", IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        try
        {
            await repository.AddAsync(document, chunks);
        }
        catch
        {
            ManagedFile.TryDelete(destination, AppPaths.DocumentsDirectory, logger);
            throw;
        }
        return document;
    }

    public Task SetEnabledAsync(long id, bool enabled) => repository.SetEnabledAsync(id, enabled);

    public async Task DeleteAsync(KnowledgeDocument document)
    {
        await repository.DeleteAsync(document.Id);
        ManagedFile.TryDelete(document.ManagedPath, AppPaths.DocumentsDirectory, logger);
    }

    public async Task<IReadOnlyList<string>> SearchAsync(string query, int maxResults = 4)
    {
        var terms = query.Split([ ' ', '\t', '\r', '\n', '，', '。', ',', '.', '!', '?', '！', '？' ], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (terms.Length == 0) return [];
        return (await repository.GetEnabledChunksAsync())
            .Select(chunk => new { chunk.Content, Score = terms.Sum(term => CountOccurrences(chunk.Content, term)) })
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).ThenByDescending(x => x.Content.Length)
            .Take(maxResults).Select(x => x.Content).ToList();
    }

    private static async Task<string> ExtractTextAsync(string path, string extension)
    {
        if (extension != ".pdf") return await File.ReadAllTextAsync(path, Encoding.UTF8);
        using var document = PdfDocument.Open(path);
        return string.Join(Environment.NewLine, document.GetPages().Select(page => page.Text));
    }

    private static IEnumerable<string> Chunk(string text)
    {
        const int size = 900, overlap = 140;
        var normalized = text.Replace("\r\n", "\n").Trim();
        var start = 0;
        while (start < normalized.Length)
        {
            var length = Math.Min(size, normalized.Length - start);
            var end = start + length;
            if (end < normalized.Length)
            {
                var boundary = normalized.LastIndexOfAny(['\n', '。', '.', ' '], end - 1, length);
                if (boundary > start + size / 2) end = boundary + 1;
            }
            yield return normalized[start..end].Trim();
            if (end >= normalized.Length) yield break;
            start = Math.Max(start + 1, end - overlap);
        }
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0; var start = 0;
        while ((start = text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase)) >= 0) { count++; start += term.Length; }
        return count;
    }
}
