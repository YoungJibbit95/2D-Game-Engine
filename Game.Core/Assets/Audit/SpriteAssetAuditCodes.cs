namespace Game.Core.Assets.Audit;

public static class SpriteAssetAuditCodes
{
    public const string MissingFile = "missing_file";
    public const string UnreadablePng = "unreadable_png";
    public const string UnsupportedImageFormat = "unsupported_image_format";
    public const string DimensionMismatch = "dimension_mismatch";
    public const string MissingGenerationBrief = "missing_generation_brief";
    public const string BriefPathMismatch = "brief_path_mismatch";
    public const string BriefSizeMismatch = "brief_size_mismatch";
    public const string IncompleteAutoTileMasks = "incomplete_autotile_masks";
}
