namespace PbOverlay.Core.GameConfig;

public sealed record GameConfigEntry(string? Section, string Key, string Value, string RawLine);
