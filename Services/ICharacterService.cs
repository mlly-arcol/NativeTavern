using NativeTavern.Models;

namespace NativeTavern.Services;

public interface ICharacterService
{
    Task<IReadOnlyList<Character>> SearchAsync(string? query, bool favoritesOnly);
    Task<IReadOnlyList<CharacterGroup>> GetGroupsAsync();
    Task CreateGroupAsync(string name, IReadOnlyCollection<long> characterIds);
    Task UpdateGroupAsync(string originalName, string name, IReadOnlyCollection<long> characterIds);
    Task DeleteGroupAsync(string name);
    Task<Character> SaveAsync(Character character, string? avatarSourcePath = null);
    Task<Character> ImportAsync(string path, CancellationToken cancellationToken = default);
    Task DeleteAsync(Character character);
}
