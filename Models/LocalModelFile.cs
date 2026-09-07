namespace NativeTavern.Models;

public sealed record LocalModelFile(string Name, string FilePath, long SizeBytes)
{
    public string DisplayName => $"{Path.GetFileNameWithoutExtension(Name)} ({SizeBytes / 1024d / 1024d / 1024d:F1} GB)";
}
