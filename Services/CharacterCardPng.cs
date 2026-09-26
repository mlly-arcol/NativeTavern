using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace NativeTavern.Services;

/// <summary>
/// Writes a character card straight into a PNG's tEXt chunks, so the shared file keeps the avatar
/// picture and can be re-imported here or by other roleplay clients.
/// </summary>
public static class CharacterCardPng
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();
    private const int PlaceholderEdge = 240;
    private const int MaxChunkBytes = 50 * 1024 * 1024;
    private const int MaxCardBytes = 20 * 1024 * 1024;

    public static async Task WriteAsync(
        string path,
        string cardJson,
        string? avatarPath,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (Encoding.UTF8.GetByteCount(cardJson) > MaxCardBytes)
            throw new InvalidDataException("角色卡内容过大，无法嵌入 PNG。");
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(cardJson));
        var chunks = TryReadChunks(avatarPath, out var avatar) ? avatar : Placeholder(name);
        await DataExportService.WriteAllBytesAtomicAsync(path, Embed(chunks, encoded), cancellationToken);
    }

    private static byte[] Embed(List<PngChunk> source, string encoded)
    {
        using var stream = new MemoryStream();
        stream.Write(Signature.AsSpan());
        foreach (var chunk in source)
        {
            if (chunk.Type == "IEND") continue;
            if (chunk.Type == "tEXt" && CardKeyword(chunk) is not null) continue;
            WriteChunk(stream, chunk.Type, chunk.Data);
        }
        WriteTextChunk(stream, "ccv3", encoded);
        WriteTextChunk(stream, "chara", encoded);
        WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static string? CardKeyword(PngChunk chunk)
    {
        var separator = Array.IndexOf(chunk.Data, (byte)0);
        if (separator <= 0) return null;
        var keyword = Encoding.ASCII.GetString(chunk.Data, 0, separator);
        return keyword is "chara" or "ccv3" ? keyword : null;
    }

    private static bool TryReadChunks(string? path, out List<PngChunk> chunks)
    {
        chunks = [];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        byte[] bytes;
        try
        {
            if (new FileInfo(path).Length > MaxChunkBytes) return false;
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return false;
        }
        if (bytes.Length < 16 || !bytes.AsSpan(0, 8).SequenceEqual(Signature)) return false;

        var offset = 8;
        var complete = false;
        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset));
            var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            if (length < 0 || length > MaxChunkBytes || offset + 12 + length > bytes.Length) return false;
            chunks.Add(new PngChunk(type, bytes.AsSpan(offset + 8, length).ToArray()));
            offset += 12 + length;
            if (type != "IEND") continue;
            complete = true;
            break;
        }
        return complete && chunks.Count > 0 && chunks[0].Type == "IHDR";
    }

    private static List<PngChunk> Placeholder(string name)
    {
        var hash = NameHash(name);
        Span<byte> header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.Slice(0, 4), PlaceholderEdge);
        BinaryPrimitives.WriteInt32BigEndian(header.Slice(4, 4), PlaceholderEdge);
        header[8] = 8;
        header[9] = 2;

        var red = (byte)(96 + hash % 96);
        var green = (byte)(96 + (hash >> 6) % 96);
        var blue = (byte)(96 + (hash >> 12) % 96);
        var row = new byte[1 + PlaceholderEdge * 3];
        for (var pixel = 1; pixel < row.Length; pixel += 3)
        {
            row[pixel] = red;
            row[pixel + 1] = green;
            row[pixel + 2] = blue;
        }

        using var raw = new MemoryStream(row.Length * PlaceholderEdge);
        for (var y = 0; y < PlaceholderEdge; y++) raw.Write(row);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, true))
        {
            raw.WriteTo(zlib);
        }
        return
        [
            new PngChunk("IHDR", header.ToArray()),
            new PngChunk("IDAT", compressed.ToArray()),
            new PngChunk("IEND", [])
        ];
    }

    private static void WriteTextChunk(Stream stream, string keyword, string text)
    {
        using var payload = new MemoryStream(Encoding.ASCII.GetByteCount(keyword) + 1 + text.Length);
        payload.Write(Encoding.ASCII.GetBytes(keyword));
        payload.WriteByte(0);
        payload.Write(Encoding.ASCII.GetBytes(text));
        WriteChunk(stream, "tEXt", payload.ToArray());
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var typeBytes = Encoding.ASCII.GetBytes(type);
        if (typeBytes.Length != 4)
            throw new InvalidDataException($"不是有效的 PNG 块类型：{type}");
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32BigEndian(header.Slice(0, 4), data.Length);
        typeBytes.CopyTo(header.Slice(4, 4));
        stream.Write(header);
        stream.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Checksum(header.Slice(4, 4), data));
        stream.Write(crc);
    }

    private static uint Checksum(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type) crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        foreach (var value in data) crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint NameHash(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value) hash = (hash ^ character) * 16777619u;
        return hash;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            table[index] = value;
        }
        return table;
    }

    private readonly record struct PngChunk(string Type, byte[] Data);
}
