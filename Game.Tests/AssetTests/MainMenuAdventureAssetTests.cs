using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Client.GameStates;
using Xunit;

namespace Game.Tests.AssetTests;

public sealed class MainMenuAdventureAssetTests
{
    [Fact]
    public void RepositoryMainMenuBackdrop_HasCompleteRuntimeMetadataAndExactBinaryEvidence()
    {
        var dataRoot = FindRepositoryGameData();
        var manifestPath = Path.Combine(dataRoot, "art_direction", "main_menu_adventure_v1_asset_manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        var runtimePath = root.GetProperty("runtimePath").GetString();
        Assert.Equal(MainMenuState.BackgroundAssetPath, runtimePath);
        Assert.True(root.GetProperty("runtimeActive").GetBoolean());
        Assert.Equal("YjsE-Project-Owned", root.GetProperty("license").GetString());

        var assetPath = Path.Combine(dataRoot, runtimePath!.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(assetPath), $"Missing main-menu backdrop: {assetPath}");
        Assert.Equal((1672, 941), ReadPngDimensions(assetPath));

        var provenancePath = Path.Combine(dataRoot, "art_direction", "main_menu_adventure_v1_provenance.json");
        using var provenance = JsonDocument.Parse(File.ReadAllText(provenancePath));
        var runtimeAsset = provenance.RootElement.GetProperty("runtimeAsset");
        Assert.Equal(1672, runtimeAsset.GetProperty("dimensions")[0].GetInt32());
        Assert.Equal(941, runtimeAsset.GetProperty("dimensions")[1].GetInt32());
        Assert.Equal(runtimeAsset.GetProperty("sha256").GetString(), ComputeSha256(assetPath));
        Assert.True(provenance.RootElement.GetProperty("runtimeLoaded").GetBoolean());

        var preview = provenance.RootElement.GetProperty("preview");
        var previewPath = Path.Combine(dataRoot, preview.GetProperty("path").GetString()!.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal((836, 471), ReadPngDimensions(previewPath));
        Assert.Equal(preview.GetProperty("sha256").GetString(), ComputeSha256(previewPath));

        var briefPath = Path.Combine(dataRoot, "asset_briefs", "main_menu_adventure_v1_brief.json");
        using var brief = JsonDocument.Parse(File.ReadAllText(briefPath));
        Assert.Equal(runtimePath, brief.RootElement.GetProperty("outputPath").GetString());
        Assert.Equal(1672, brief.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(941, brief.RootElement.GetProperty("height").GetInt32());
        Assert.NotEmpty(brief.RootElement.GetProperty("requirements").EnumerateArray());
    }

    private static (int Width, int Height) ReadPngDimensions(string path)
    {
        Span<byte> header = stackalloc byte[24];
        using var stream = File.OpenRead(path);
        stream.ReadExactly(header);
        Assert.Equal(0x89504E470D0A1A0AUL, BinaryPrimitives.ReadUInt64BigEndian(header[..8]));
        return (
            BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4)));
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string FindRepositoryGameData()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Game.Data");
            if (File.Exists(Path.Combine(directory.FullName, "YjsE.sln")) &&
                Directory.Exists(Path.Combine(candidate, "assets")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository Game.Data directory.");
    }
}
