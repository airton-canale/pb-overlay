using System.Text;

namespace PbOverlay.Core.GameConfig;

public sealed class IniParser : IGameConfigParser
{
    public string FormatName => "ini";

    public bool CanParse(ReadOnlySpan<byte> firstBytes)
    {
        // Scan for first non-whitespace char. If '[', it's a section header → INI.
        // Otherwise, look at the first non-comment line: if it contains '=' before a newline
        // and doesn't start with ';' or '#', treat as INI.
        int i = 0;
        while (i < firstBytes.Length && IsAsciiWhitespace(firstBytes[i]))
        {
            i++;
        }
        if (i >= firstBytes.Length)
        {
            return false;
        }
        if (firstBytes[i] == (byte)'[')
        {
            return true;
        }

        // Walk lines, skipping comments/blanks; check if any line has '=' before newline.
        while (i < firstBytes.Length)
        {
            byte b = firstBytes[i];
            if (b == (byte)';' || b == (byte)'#')
            {
                // skip to end of line
                while (i < firstBytes.Length && firstBytes[i] != (byte)'\n')
                {
                    i++;
                }
                i++;
                continue;
            }
            if (b == (byte)'\r' || b == (byte)'\n' || b == (byte)' ' || b == (byte)'\t')
            {
                i++;
                continue;
            }

            // Non-comment, non-whitespace: scan line for '='.
            bool sawEq = false;
            while (i < firstBytes.Length && firstBytes[i] != (byte)'\n' && firstBytes[i] != (byte)'\r')
            {
                if (firstBytes[i] == (byte)'=')
                {
                    sawEq = true;
                    break;
                }
                i++;
            }
            return sawEq;
        }
        return false;
    }

    public IEnumerable<GameConfigEntry> Parse(byte[] fileContents)
    {
        string text = Encoding.UTF8.GetString(fileContents);
        string? section = null;
        string[] lines = text.Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            if (trimmed[0] == ';' || trimmed[0] == '#')
            {
                continue;
            }
            if (trimmed[0] == '[' && trimmed[^1] == ']')
            {
                section = trimmed[1..^1].Trim();
                continue;
            }
            int eq = line.IndexOf('=');
            if (eq < 0)
            {
                continue;
            }
            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            if (key.Length == 0)
            {
                continue;
            }
            yield return new GameConfigEntry(section, key, value, rawLine);
        }
    }

    private static bool IsAsciiWhitespace(byte b)
    {
        return b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n';
    }
}
