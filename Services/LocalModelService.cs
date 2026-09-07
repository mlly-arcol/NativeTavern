using System.Diagnostics;
using NativeTavern.Models;

namespace NativeTavern.Services;

public sealed class LocalModelService : IDisposable
{
    public const string DefaultModelDirectory = @"E:\BaiduNetdiskDownload\SillyTavern\models";
    public const string DefaultKoboldCppPath = @"E:\BaiduNetdiskDownload\SillyTavern\KoboldCpp\koboldcpp.exe";
    private static readonly Uri ModelsEndpoint = new("http://localhost:5001/v1/models");
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

    public async Task StartAsync(string koboldCppPath, LocalModelFile model, CancellationToken cancellationToken)
    {
        if (!File.Exists(koboldCppPath))
            throw new FileNotFoundException("KoboldCpp executable was not found.", koboldCppPath);
        if (!File.Exists(model.FilePath))
            throw new FileNotFoundException("GGUF model file was not found.", model.FilePath);

        StopOwnedProcess();
        var startInfo = new ProcessStartInfo
        {
            FileName = koboldCppPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var argument in new[]
                 {
                     "--model", model.FilePath, "--port", "5001", "--usecuda", "--gpulayers", "99",
                     "--contextsize", "8192", "--reasoningeffort", "none"
                 })
            startInfo.ArgumentList.Add(argument);

        _ownedProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("KoboldCpp could not be started.");
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_ownedProcess.HasExited)
                throw new InvalidOperationException($"KoboldCpp exited with code {_ownedProcess.ExitCode}.");
            try
            {
                using var response = await _client.GetAsync(ModelsEndpoint, cancellationToken);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(1000, cancellationToken);
        }
        throw new TimeoutException("KoboldCpp did not become ready within two minutes.");
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
