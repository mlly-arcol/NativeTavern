using NativeTavern.Data.Repositories;
using NativeTavern.Helpers;
using NativeTavern.Models;

namespace NativeTavern.Services;

public sealed class AttachmentService(ChatAttachmentRepository repository)
{
    private const long MaxImageSize = 10 * 1024 * 1024;
    public static bool IsSupportedImage(string path) => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif";

    public async Task<IReadOnlyList<ChatAttachment>> SaveImagesAsync(long messageId, IEnumerable<string> sourcePaths)
    {
        var saved = new List<ChatAttachment>();
        foreach (var source in sourcePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(source) || !IsSupportedImage(source)) throw new InvalidDataException("仅支持 PNG、JPG、WEBP 和 GIF 图片附件。");
            var info = new FileInfo(source);
            if (info.Length > MaxImageSize) throw new InvalidDataException("单张图片不能超过 10 MB。");
            var extension = Path.GetExtension(source).ToLowerInvariant();
            var destination = Path.Combine(AppPaths.AttachmentsDirectory, $"{Guid.NewGuid():N}{extension}");
            File.Copy(source, destination, false);
            var item = new ChatAttachment { ChatMessageId=messageId, FileName=Path.GetFileName(source), FilePath=destination, MimeType=MimeType(extension), SizeBytes=info.Length, CreatedAt=DateTimeOffset.UtcNow };
            await repository.AddAsync(item);
            saved.Add(item);
        }
        return saved;
    }

    public Task<IReadOnlyList<ChatAttachment>> GetByMessageAsync(long messageId) => repository.GetByMessageAsync(messageId);

    public static async Task<IReadOnlyList<string>> ToDataUrlsAsync(IEnumerable<ChatAttachment> attachments)
    {
        var urls = new List<string>();
        foreach (var attachment in attachments.Where(x => File.Exists(x.FilePath)))
            urls.Add($"data:{attachment.MimeType};base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(attachment.FilePath))}");
        return urls;
    }

    private static string MimeType(string extension) => extension switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif", _ => "application/octet-stream"
    };
}
