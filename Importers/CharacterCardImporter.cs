using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using NativeTavern.Models;

namespace NativeTavern.Importers;

public sealed class CharacterCardImporter
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public async Task<Character> ImportAsync(string path, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var json = extension switch
        {
            ".json" => await File.ReadAllTextAsync(path, cancellationToken),
            ".png" => await ReadPngCardJsonAsync(path, cancellationToken),
            _ => throw new InvalidDataException("仅支持 PNG 和 JSON 角色卡。")
        };
        return ParseJson(json);
    }

    public Character ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var data = root.TryGetProperty("data", out var nested) && nested.ValueKind == JsonValueKind.Object
            ? nested : root;
        var character = new Character
        {
            Name = ReadString(data, "name"),
            Description = ReadString(data, "description"),
            Personality = ReadString(data, "personality"),
            Scenario = ReadString(data, "scenario"),
            FirstMessage = ReadString(data, "first_mes"),
            ExampleMessages = ReadString(data, "mes_example"),
            Creator = ReadString(data, "creator"),
            Tags = ReadTags(data)
        };
        if (string.IsNullOrWhiteSpace(character.Name))
            throw new InvalidDataException("角色卡缺少 name 字段。");
        return character;
    }

    private static async Task<string> ReadPngCardJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var signature = new byte[8];
        await stream.ReadExactlyAsync(signature, cancellationToken);
        if (!signature.SequenceEqual(PngSignature))
            throw new InvalidDataException("文件不是有效的 PNG。");

        string? v2 = null;
        string? v3 = null;
        var header = new byte[8];
        while (stream.Position < stream.Length)
        {
            await stream.ReadExactlyAsync(header, cancellationToken);
            var length = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            var type = Encoding.ASCII.GetString(header, 4, 4);
            if (length > 64 * 1024 * 1024)
                throw new InvalidDataException("PNG 元数据块过大。");
            var data = new byte[length];
            await stream.ReadExactlyAsync(data, cancellationToken);
            stream.Seek(4, SeekOrigin.Current);
            if (type == "tEXt")
            {
                var separator = Array.IndexOf(data, (byte)0);
                if (separator > 0)
                {
                    var keyword = Encoding.ASCII.GetString(data, 0, separator);
                    if (keyword is "chara" or "ccv3")
                    {
                        var encoded = Encoding.ASCII.GetString(data, separator + 1, data.Length - separator - 1);
                        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Trim()));
                        if (keyword == "ccv3") v3 = decoded; else v2 = decoded;
                    }
                }
            }
            if (type == "IEND") break;
        }
        return v3 ?? v2 ?? throw new InvalidDataException("PNG 中没有找到 chara 或 ccv3 角色卡数据。");
    }

    private static string ReadString(JsonElement data, string name) =>
        data.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;

    private static string ReadTags(JsonElement data)
    {
        if (!data.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            return string.Empty;
        return string.Join(", ", tags.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
