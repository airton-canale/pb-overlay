using Microsoft.Extensions.Logging;

namespace PbOverlay.Core.GameConfig;

// ponytail: read-only reader by design. A writer is intentionally absent until the real
// PB config format and the anti-cheat write policy are confirmed. Add a writer only after
// both are known — until then, mutating the game's config from here is a footgun.
public sealed class AutoDetectGameConfigReader
{
    private readonly ILogger<AutoDetectGameConfigReader>? _log;
    private readonly List<IGameConfigParser> _parsers;

    public AutoDetectGameConfigReader(ILogger<AutoDetectGameConfigReader>? log = null)
    {
        _log = log;
        _parsers = new List<IGameConfigParser>
        {
            new IniParser(),
            new JsonParser(),
            new XmlParser(),
        };
    }

    public GameConfigReadResult Read(string path)
    {
        byte[] bytes;
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var ms = new MemoryStream())
        {
            fs.CopyTo(ms);
            bytes = ms.ToArray();
        }

        DateTime mtime = File.GetLastWriteTimeUtc(path);
        int previewLen = Math.Min(256, bytes.Length);
        string hex = Convert.ToHexString(bytes.AsSpan(0, previewLen));
        _log?.LogDebug("Read game config {Path} ({Length} bytes)", path, bytes.Length);

        ReadOnlySpan<byte> head = bytes.AsSpan(0, previewLen);
        IReadOnlyList<GameConfigEntry> entries = Array.Empty<GameConfigEntry>();
        string format = "unknown";

        foreach (IGameConfigParser parser in _parsers)
        {
            if (!parser.CanParse(head))
            {
                continue;
            }

            format = parser.FormatName;
            var collected = new List<GameConfigEntry>();
            try
            {
                foreach (GameConfigEntry entry in parser.Parse(bytes))
                {
                    collected.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _log?.LogWarning(ex, "Parser {Format} failed partway through {Path}", parser.FormatName, path);
            }
            entries = collected;
            break;
        }

        return new GameConfigReadResult(path, mtime, format, entries, hex);
    }
}
