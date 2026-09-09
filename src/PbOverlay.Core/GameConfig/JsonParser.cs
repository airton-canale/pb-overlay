using System.Text.Json;

namespace PbOverlay.Core.GameConfig;

public sealed class JsonParser : IGameConfigParser
{
    public string FormatName => "json";

    public bool CanParse(ReadOnlySpan<byte> firstBytes)
    {
        foreach (byte b in firstBytes)
        {
            if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n')
            {
                continue;
            }
            return b == (byte)'{' || b == (byte)'[';
        }
        return false;
    }

    public IEnumerable<GameConfigEntry> Parse(byte[] fileContents)
    {
        // JsonDocument disposes need to be materialized before yielding across the boundary,
        // so collect into a list first.
        var results = new List<GameConfigEntry>();
        using JsonDocument doc = JsonDocument.Parse(fileContents);
        Walk(doc.RootElement, "", results);
        return results;
    }

    private static void Walk(JsonElement element, string path, List<GameConfigEntry> results)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    string childPath = path.Length == 0 ? prop.Name : path + "." + prop.Name;
                    Walk(prop.Value, childPath, results);
                }
                break;

            case JsonValueKind.Array:
                int idx = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    string childPath = path + "[" + idx + "]";
                    Walk(item, childPath, results);
                    idx++;
                }
                break;

            default:
                EmitLeaf(element, path, results);
                break;
        }
    }

    private static void EmitLeaf(JsonElement element, string path, List<GameConfigEntry> results)
    {
        string? section;
        string key;
        int dot = path.LastIndexOf('.');
        if (dot < 0)
        {
            section = null;
            key = path;
        }
        else
        {
            section = path[..dot];
            key = path[(dot + 1)..];
        }

        string value = element.ValueKind switch
        {
            JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
            _ => element.ToString(),
        };

        results.Add(new GameConfigEntry(section, key, value, path + "=" + value));
    }
}
