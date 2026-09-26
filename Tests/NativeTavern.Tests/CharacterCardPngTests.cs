using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using NativeTavern.Importers;
using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

/// <summary>
/// A card PNG has to survive two readers at once: our own importer and whatever image viewer the
/// user shares the file with, so the chunk layout is checked against the PNG spec here.
/// </summary>
public sealed class CharacterCardPngTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nt-png-card-" + Guid.NewGuid().ToString("N"));

    public CharacterCardPngTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task PngCardRoundTripsThroughTheImporter()
    {
        var path = await ExportAsync(new Character
        {
            Name = "云梦泽",
            Description = "守墓人一族的最后血脉",
            Personality = "寡言，重诺",
            Scenario = "荒年古墓",
            FirstMessage = "你踩到我的界碑了。",
            ExampleMessages = "<example>不该动。</example>",
            Creator = "tang",
            Tags = "仙侠, 守墓"
        });

        var imported = await new CharacterCardImporter().ImportAsync(path);

        Assert.Equal("云梦泽", imported.Name);
        Assert.Equal("守墓人一族的最后血脉", imported.Description);
        Assert.Equal("寡言，重诺", imported.Personality);
        Assert.Equal("荒年古墓", imported.Scenario);
        Assert.Equal("你踩到我的界碑了。", imported.FirstMessage);
        Assert.Equal("tang", imported.Creator);
        Assert.Equal("仙侠, 守墓", imported.Tags);
    }

    [Fact]
    public async Task PngKeepsOneFreshCardChunkPairAndADecodableImage()
    {
        var path = await ExportAsync(new Character { Name = "苏辞", Description = "旧城门吏" });
        var chunks = ReadChunks(path);

        Assert.Equal(new[] { "IHDR", "IDAT", "tEXt", "tEXt", "IEND" }, chunks.Select(c => c.Type));
        Assert.Equal(new[] { "ccv3", "chara" }, chunks.Where(c => c.Type == "tEXt").Select(Keyword));
        Assert.All(chunks, chunk => Assert.Equal(chunk.Crc, ExpectedCrc(chunk)));

        var ihdr = chunks.First(c => c.Type == "IHDR").Data;
        var width = BinaryPrimitives.ReadInt32BigEndian(ihdr.AsSpan(0, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(ihdr.AsSpan(4, 4));
        Assert.Equal(240, width);
        Assert.Equal(8, ihdr[8]);
        Assert.Equal(2, ihdr[9]);

        await using var inflate = new ZLibStream(
            new MemoryStream(chunks.First(c => c.Type == "IDAT").Data),
            CompressionMode.Decompress);
        using var decompressed = new MemoryStream();
        await inflate.CopyToAsync(decompressed);
        Assert.Equal((long)height * (1 + width * 3), decompressed.Length);
    }

    [Fact]
    public async Task AvatarArtIsKeptAndAnOlderEmbeddedCardIsDropped()
    {
        var avatar = await ExportAsync(new Character { Name = "前身", Description = "旧卡数据" });
        var path = Path.Combine(_directory, "shared.png");
        await DataExportService.ExportCharacterAsync(
            new Character { Name = "新身", Description = "改写后的设定", AvatarPath = avatar }, path);

        var chunks = ReadChunks(path);
        Assert.Equal(2, chunks.Count(c => c.Type == "tEXt"));
        Assert.Equal("新身", (await new CharacterCardImporter().ImportAsync(path)).Name);
    }

    [Fact]
    public async Task NonPngAvatarFallsBackToAGeneratedImage()
    {
        var notPng = Path.Combine(_directory, "avatar.txt");
        await File.WriteAllTextAsync(notPng, "这不是图片");
        var path = Path.Combine(_directory, "fallback.png");

        await DataExportService.ExportCharacterAsync(
            new Character { Name = "陆沉", AvatarPath = notPng }, path);

        var chunks = ReadChunks(path);
        Assert.Equal("IHDR", chunks[0].Type);
        Assert.Equal(2, chunks.Count(c => c.Type == "tEXt"));
        Assert.Equal("陆沉", (await new CharacterCardImporter().ImportAsync(path)).Name);
    }

    [Fact]
    public async Task OtherExtensionsAreStillRejected()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DataExportService.ExportCharacterAsync(new Character { Name = "甲" }, Path.Combine(_directory, "card.txt")));
    }

    private async Task<string> ExportAsync(Character character)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.png");
        await DataExportService.ExportCharacterAsync(character, path);
        Assert.StartsWith("PNG", Encoding.ASCII.GetString(await File.ReadAllBytesAsync(path), 1, 3));
        return path;
    }

    private static List<Chunk> ReadChunks(string path)
    {
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.AsSpan(0, 8).ToArray());
        var result = new List<Chunk>();
        var offset = 8;
        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset));
            var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            var data = bytes.AsSpan(offset + 8, length).ToArray();
            result.Add(new Chunk(type, data, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + length, 4))));
            offset += 12 + length;
            if (type == "IEND") break;
        }
        return result;
    }

    private static string Keyword(Chunk chunk) =>
        Encoding.ASCII.GetString(chunk.Data, 0, Array.IndexOf(chunk.Data, (byte)0));

    /// <summary>Recomputed straight from the spec: CRC-32 over the type and data fields.</summary>
    private static uint ExpectedCrc(Chunk chunk)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in Encoding.ASCII.GetBytes(chunk.Type).Concat(chunk.Data))
        {
            var current = crc ^ value;
            for (var bit = 0; bit < 8; bit++)
                current = (current & 1) != 0 ? (current >> 1) ^ 0xEDB88320u : current >> 1;
            crc = current;
        }
        return crc ^ 0xFFFFFFFFu;
    }

    private sealed record Chunk(string Type, byte[] Data, uint Crc);

    public void Dispose()
    {
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }
}
