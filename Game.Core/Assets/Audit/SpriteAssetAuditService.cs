using Game.Core.Assets.Generation;

namespace Game.Core.Assets.Audit;

public sealed class SpriteAssetAuditService
{
    public SpriteAssetAuditReport Audit(
        string contentRoot,
        SpriteAssetRegistry assets,
        SpriteGenerationBriefRegistry? generationBriefs = null,
        SpriteAssetAuditOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        ArgumentNullException.ThrowIfNull(assets);
        options ??= new SpriteAssetAuditOptions();

        var entries = new List<SpriteAssetAuditEntry>();
        var issues = new List<SpriteAssetAuditIssue>();
        var fallbackId = assets.Fallback.Id;

        foreach (var asset in assets.Definitions.OrderBy(asset => asset.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (options.SkipFallbackSprite && string.Equals(asset.Id, fallbackId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var resolvedPath = ResolvePath(contentRoot, asset.Path);
            var fileRead = ReadPngDimensions(resolvedPath);
            var fileStatus = fileRead.Status;
            SpriteGenerationBrief? brief = null;
            var hasBrief = generationBriefs is not null && generationBriefs.TryGetBySpriteId(asset.Id, out brief);
            var briefPathMatches = !hasBrief || PathsMatch(asset.Path, brief!.OutputPath);
            var briefSizeMatches = !hasBrief || (brief!.Width == asset.Width && brief.Height == asset.Height);
            var completeMasks = HasCompleteAutoTileMasks(asset);

            var entry = new SpriteAssetAuditEntry(
                asset.Id,
                asset.Path,
                resolvedPath,
                asset.Category,
                asset.Width,
                asset.Height,
                fileStatus,
                fileRead.Width,
                fileRead.Height,
                hasBrief,
                briefPathMatches,
                briefSizeMatches,
                completeMasks);
            entries.Add(entry);

            AddFileIssues(asset, entry, fileRead, options, issues);
            AddBriefIssues(asset, entry, options, issues);
        }

        return new SpriteAssetAuditReport(entries, issues);
    }

    private static void AddFileIssues(
        SpriteAssetDefinition asset,
        SpriteAssetAuditEntry entry,
        PngDimensionReadResult fileRead,
        SpriteAssetAuditOptions options,
        List<SpriteAssetAuditIssue> issues)
    {
        if (entry.FileStatus == SpriteAssetFileStatus.Missing)
        {
            issues.Add(new SpriteAssetAuditIssue(
                options.TreatMissingFilesAsErrors ? SpriteAssetAuditSeverity.Error : SpriteAssetAuditSeverity.Warning,
                asset.Id,
                SpriteAssetAuditCodes.MissingFile,
                $"Sprite asset '{asset.Id}' is missing file '{entry.ManifestPath}'."));
            return;
        }

        if (entry.FileStatus == SpriteAssetFileStatus.Unreadable)
        {
            issues.Add(new SpriteAssetAuditIssue(
                SpriteAssetAuditSeverity.Error,
                asset.Id,
                fileRead.Code,
                $"Sprite asset '{asset.Id}' could not be read as PNG: {fileRead.Message}"));
            return;
        }

        if (entry.ActualWidth != asset.Width || entry.ActualHeight != asset.Height)
        {
            issues.Add(new SpriteAssetAuditIssue(
                SpriteAssetAuditSeverity.Error,
                asset.Id,
                SpriteAssetAuditCodes.DimensionMismatch,
                $"Sprite asset '{asset.Id}' declares {asset.Width}x{asset.Height}, but file is {entry.ActualWidth}x{entry.ActualHeight}."));
        }
    }

    private static void AddBriefIssues(
        SpriteAssetDefinition asset,
        SpriteAssetAuditEntry entry,
        SpriteAssetAuditOptions options,
        List<SpriteAssetAuditIssue> issues)
    {
        if (options.RequireGenerationBriefs && !entry.HasGenerationBrief)
        {
            issues.Add(new SpriteAssetAuditIssue(
                SpriteAssetAuditSeverity.Warning,
                asset.Id,
                SpriteAssetAuditCodes.MissingGenerationBrief,
                $"Sprite asset '{asset.Id}' has no generation brief."));
        }

        if (!entry.GenerationBriefPathMatches)
        {
            issues.Add(new SpriteAssetAuditIssue(
                SpriteAssetAuditSeverity.Error,
                asset.Id,
                SpriteAssetAuditCodes.BriefPathMismatch,
                $"Sprite generation brief for '{asset.Id}' does not write to '{asset.Path}'."));
        }

        if (!entry.GenerationBriefSizeMatches)
        {
            issues.Add(new SpriteAssetAuditIssue(
                SpriteAssetAuditSeverity.Error,
                asset.Id,
                SpriteAssetAuditCodes.BriefSizeMismatch,
                $"Sprite generation brief for '{asset.Id}' does not match declared size {asset.Width}x{asset.Height}."));
        }

        if (options.RequireCompleteAutoTileMasks && asset.HasTag("autotile") && !entry.HasCompleteAutoTileMasks)
        {
            issues.Add(new SpriteAssetAuditIssue(
                SpriteAssetAuditSeverity.Error,
                asset.Id,
                SpriteAssetAuditCodes.IncompleteAutoTileMasks,
                $"Autotile sprite '{asset.Id}' must provide source frames for masks 0..15."));
        }
    }

    private static bool HasCompleteAutoTileMasks(SpriteAssetDefinition asset)
    {
        if (!asset.HasTag("autotile"))
        {
            return true;
        }

        var masks = asset.Frames
            .Where(frame => frame.AutoTileMask.HasValue)
            .Select(frame => frame.AutoTileMask!.Value)
            .ToHashSet();

        for (var mask = 0; mask <= 15; mask++)
        {
            if (!masks.Contains(mask))
            {
                return false;
            }
        }

        return true;
    }

    private static string ResolvePath(string contentRoot, string manifestPath)
    {
        return Path.GetFullPath(Path.IsPathRooted(manifestPath)
            ? manifestPath
            : Path.Combine(contentRoot, manifestPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool PathsMatch(string left, string right)
    {
        return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim('/');
    }

    private static PngDimensionReadResult ReadPngDimensions(string path)
    {
        if (!File.Exists(path))
        {
            return PngDimensionReadResult.Missing;
        }

        Span<byte> buffer = stackalloc byte[24];
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Read(buffer) < buffer.Length)
            {
                return PngDimensionReadResult.Unreadable(SpriteAssetAuditCodes.UnreadablePng, "file is shorter than a PNG header.");
            }
        }
        catch (IOException exception)
        {
            return PngDimensionReadResult.Unreadable(SpriteAssetAuditCodes.UnreadablePng, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return PngDimensionReadResult.Unreadable(SpriteAssetAuditCodes.UnreadablePng, exception.Message);
        }

        ReadOnlySpan<byte> signature = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (!buffer[..8].SequenceEqual(signature) ||
            buffer[12] != (byte)'I' ||
            buffer[13] != (byte)'H' ||
            buffer[14] != (byte)'D' ||
            buffer[15] != (byte)'R')
        {
            return PngDimensionReadResult.Unreadable(SpriteAssetAuditCodes.UnsupportedImageFormat, "expected PNG signature and IHDR chunk.");
        }

        var width = ReadBigEndianInt32(buffer[16..20]);
        var height = ReadBigEndianInt32(buffer[20..24]);
        if (width <= 0 || height <= 0)
        {
            return PngDimensionReadResult.Unreadable(SpriteAssetAuditCodes.UnreadablePng, "PNG dimensions are not positive.");
        }

        return PngDimensionReadResult.Present(width, height);
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> bytes)
    {
        return (bytes[0] << 24) |
               (bytes[1] << 16) |
               (bytes[2] << 8) |
               bytes[3];
    }

    private sealed record PngDimensionReadResult(
        SpriteAssetFileStatus Status,
        int? Width,
        int? Height,
        string Code,
        string Message)
    {
        public static PngDimensionReadResult Missing { get; } = new(
            SpriteAssetFileStatus.Missing,
            Width: null,
            Height: null,
            SpriteAssetAuditCodes.MissingFile,
            "file does not exist.");

        public static PngDimensionReadResult Present(int width, int height)
        {
            return new PngDimensionReadResult(
                SpriteAssetFileStatus.Present,
                width,
                height,
                Code: string.Empty,
                Message: string.Empty);
        }

        public static PngDimensionReadResult Unreadable(string code, string message)
        {
            return new PngDimensionReadResult(
                SpriteAssetFileStatus.Unreadable,
                Width: null,
                Height: null,
                code,
                message);
        }
    }
}
