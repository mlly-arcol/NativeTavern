using Microsoft.Extensions.Logging;
using NativeTavern.Data.Repositories;
using NativeTavern.Helpers;
using NativeTavern.Importers;
using NativeTavern.Models;

namespace NativeTavern.Services;

public sealed class CharacterService(
    CharacterRepository repository,
    CharacterCardImporter importer,
    ILogger<CharacterService> logger)
{
    public Task<IReadOnlyList<Character>> SearchAsync(string? query, bool favoritesOnly) =>
        repository.SearchAsync(query, favoritesOnly);

    public async Task<Character> SaveAsync(Character character, string? avatarSourcePath = null)
    {
        if (string.IsNullOrWhiteSpace(character.Name))
            throw new InvalidOperationException("角色名称不能为空。");
        if (!string.IsNullOrWhiteSpace(avatarSourcePath) && File.Exists(avatarSourcePath) &&
            !IsManagedAvatar(avatarSourcePath))
        {
            var previousAvatar = character.AvatarPath;
            character.AvatarPath = CopyAvatar(avatarSourcePath);
            if (!string.IsNullOrWhiteSpace(previousAvatar) &&
                IsManagedAvatar(previousAvatar) && File.Exists(previousAvatar))
            {
                File.Delete(previousAvatar);
            }
        }
        character.UpdatedAt = DateTimeOffset.UtcNow;
        if (character.Id == 0)
        {
            character.CreatedAt = character.UpdatedAt;
            await repository.CreateAsync(character);
        }
        else
        {
            await repository.UpdateAsync(character);
        }
        return character;
    }

    public async Task<Character> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        var character = await importer.ImportAsync(path, cancellationToken);
        if (Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
            character.AvatarPath = CopyAvatar(path);
        await SaveAsync(character);
        logger.LogInformation("Imported character card {CharacterName}.", character.Name);
        return character;
    }

    public async Task DeleteAsync(Character character)
    {
        await repository.DeleteAsync(character.Id);
        if (!string.IsNullOrWhiteSpace(character.AvatarPath) &&
            IsManagedAvatar(character.AvatarPath) && File.Exists(character.AvatarPath))
        {
            File.Delete(character.AvatarPath);
        }
        logger.LogInformation("Deleted character {CharacterId}.", character.Id);
    }

    private static string CopyAvatar(string source)
    {
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new InvalidDataException("头像仅支持 PNG、JPG、JPEG 或 WebP。");
        var destination = Path.Combine(AppPaths.AvatarsDirectory, $"{Guid.NewGuid():N}{extension}");
        File.Copy(source, destination, false);
        return destination;
    }

    private static bool IsManagedAvatar(string path)
    {
        var relative = Path.GetRelativePath(
            Path.GetFullPath(AppPaths.AvatarsDirectory),
            Path.GetFullPath(path));
        return !Path.IsPathRooted(relative) &&
               relative != ".." &&
               !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
