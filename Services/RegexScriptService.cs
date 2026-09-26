using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;

namespace NativeTavern.Services;

/// <summary>
/// Runs user-authored find/replace scripts over message text. Compiled patterns are cached until a
/// script changes, and every regex carries a match timeout so a bad pattern cannot hang generation.
/// </summary>
public sealed class RegexScriptService(RegexScriptRepository repository, ILogger<RegexScriptService> logger)
{
    internal static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(400);

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private IReadOnlyList<RegexScript> _scripts = [];
    private Dictionary<RegexScriptTarget, IReadOnlyList<CompiledScript>>? _cache;
    private bool _loaded;

    /// <summary>A script reduced to what the replacement step needs, with an unusable pattern left null.</summary>
    public sealed record CompiledScript(
        RegexScriptTarget Target,
        bool IsEnabled,
        RegexScriptMode Mode,
        string Pattern,
        string Replacement,
        Regex? Regex)
    {
        public static CompiledScript Compile(RegexScript script) => new(
            script.Target,
            script.IsEnabled,
            script.Mode,
            script.Pattern,
            script.Replacement,
            script.Mode == RegexScriptMode.Regex ? Build(script.Pattern) : null);
    }

    public async Task<IReadOnlyList<RegexScript>> GetScriptsAsync()
    {
        if (!_loaded) await ReloadAsync();
        return _scripts;
    }

    public async Task ReloadAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            _scripts = await repository.GetAllAsync();
            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task SaveAsync(RegexScript script)
    {
        await repository.SaveAsync(script);
        Invalidate();
    }

    public async Task DeleteAsync(long id)
    {
        await repository.DeleteAsync(id);
        Invalidate();
    }

    public async Task<string> ApplyAsync(string text, RegexScriptTarget target)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var scripts = await GetCompiledAsync(target);
        return scripts.Count == 0 ? text : Apply(text, scripts, target);
    }

    /// <summary>
    /// Compiled scripts per target. The chat path transforms every streamed paragraph, so compiling
    /// once per change keeps a hot loop free of regex construction.
    /// </summary>
    private async Task<IReadOnlyList<CompiledScript>> GetCompiledAsync(RegexScriptTarget target)
    {
        var cache = _cache;
        if (cache is null)
        {
            var scripts = await GetScriptsAsync();
            cache = scripts
                .GroupBy(x => x.Target)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<CompiledScript>)group
                        .Where(x => x.IsEnabled && x.Pattern.Length > 0)
                        .Select(CompileLogged)
                        .Where(x => x is not null)
                        .Select(x => x!)
                        .ToList());
            _cache = cache;
        }

        return cache.TryGetValue(target, out var compiled) ? compiled : [];
    }

    private CompiledScript? CompileLogged(RegexScript script)
    {
        var compiled = CompiledScript.Compile(script);
        if (compiled.Regex is null && script.Mode == RegexScriptMode.Regex)
            logger.LogWarning("Regex script {ScriptName} was skipped because its pattern is invalid.", script.Name);
        return compiled;
    }

    public static string Apply(string text, IEnumerable<CompiledScript> scripts, RegexScriptTarget target)
    {
        foreach (var script in scripts)
        {
            if (!script.IsEnabled || script.Target != target) continue;
            text = script.Mode == RegexScriptMode.Literal
                ? text.Replace(script.Pattern, script.Replacement, StringComparison.Ordinal)
                : script.Regex?.Replace(text, script.Replacement) ?? text;
        }

        return text;
    }

    /// <summary>Returns the compile error for a pattern, or null when it is usable.</summary>
    public static string? ValidatePattern(RegexScriptMode mode, string pattern)
    {
        if (mode == RegexScriptMode.Literal || string.IsNullOrEmpty(pattern)) return null;
        try
        {
            _ = new Regex(pattern, DefaultOptions, MatchTimeout);
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    internal const RegexOptions DefaultOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static Regex? Build(string pattern)
    {
        try
        {
            return new Regex(pattern, DefaultOptions, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void Invalidate()
    {
        _loaded = false;
        _cache = null;
    }
}
