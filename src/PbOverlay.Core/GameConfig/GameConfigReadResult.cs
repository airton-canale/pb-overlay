namespace PbOverlay.Core.GameConfig;

public sealed record GameConfigReadResult(
    string FilePath,
    DateTime LastModifiedUtc,
    string DetectedFormat,
    IReadOnlyList<GameConfigEntry> Entries,
    string First256BytesHex);
