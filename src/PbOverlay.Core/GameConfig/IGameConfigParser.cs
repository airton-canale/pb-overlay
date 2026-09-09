namespace PbOverlay.Core.GameConfig;

public interface IGameConfigParser
{
    bool CanParse(ReadOnlySpan<byte> firstBytes);
    IEnumerable<GameConfigEntry> Parse(byte[] fileContents);
    string FormatName { get; }
}
