using System.Diagnostics;
using NativeTavern.Models;

namespace NativeTavern.Services;

public sealed class LocalModelService : IDisposable
{
    public static string DefaultModelDirectory => Path.Combine(AppContext.BaseDirectory, "LocalModels");
    public const string LegacyModelDirectory = @"E:\BaiduNetdiskDownload\SillyTavern\models";
    public const string DefaultModelFileName = "Qwen3-8B-Q4_K_M.gguf";
    public static string DefaultLlamaCppPath => Path.Combine(AppContext.BaseDirectory, "llama.cpp", "llama-server.exe");
    private static readonly Uri ModelsEndpoint = new("http://127.0.0.1:8080/v1/models");
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(3) };
    private Process? _ownedProcess;

    public IReadOnlyList<LocalModelFile> Scan(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return Directory.EnumerateFiles(directory, "*.gguf", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new LocalModelFile(file.Name, file.FullName, file.Length))
            .ToList();
    }

    public async Task StartAsync(string llamaCppPath, LocalModelFile model, CancellationToken cancellationToken)
    {
        if (!File.Exists(llamaCppPath))
            throw new FileNotFoundException("llama.cpp server executable was not found.", llamaCppPath);
        if (!File.Exists(model.FilePath))
            throw new FileNotFoundException("GGUF model file was not found.", model.FilePath);

        StopOwnedProcess();
        var startInfo = new ProcessStartInfo
        {
            FileName = llamaCppPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in new[]
                 {
                     "--model", model.FilePath, "--host", "127.0.0.1", "--port", "8080",
                     "--ctx-size", "8192", "--n-gpu-layers", "35", "--jinja",
                     "--alias", "NativeTavern-Qwen3"
                 })
            startInfo.ArgumentList.Add(argument);

        _ownedProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("llama.cpp server could not be started.");
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_ownedProcess.HasExited)
                throw new InvalidOperationException($"llama.cpp server exited with code {_ownedProcess.ExitCode}.");
            try
            {
                using var response = await _client.GetAsync(ModelsEndpoint, cancellationToken);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(1000, cancellationToken);
        }
        throw new TimeoutException("llama.cpp server did not become ready within two minutes.");
    }

    private void StopOwnedProcess()
    {
        if (_ownedProcess is null) return;
        try
        {
            if (!_ownedProcess.HasExited) _ownedProcess.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        finally
        {
            _ownedProcess.Dispose();
            _ownedProcess = null;
        }
    }

    public void Dispose()
    {
        StopOwnedProcess();
        _client.Dispose();
    }
}
