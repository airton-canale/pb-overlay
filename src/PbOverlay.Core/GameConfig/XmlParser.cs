using System.Text;
using System.Xml.Linq;

namespace PbOverlay.Core.GameConfig;

public sealed class XmlParser : IGameConfigParser
{
    public string FormatName => "xml";

    public bool CanParse(ReadOnlySpan<byte> firstBytes)
    {
        foreach (byte b in firstBytes)
        {
            if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n' || b == 0xEF || b == 0xBB || b == 0xBF)
            {
                // skip whitespace + UTF-8 BOM bytes
                continue;
            }
            return b == (byte)'<';
        }
        return false;
    }

    public IEnumerable<GameConfigEntry> Parse(byte[] fileContents)
    {
        var results = new List<GameConfigEntry>();
        XDocument doc = XDocument.Parse(Encoding.UTF8.GetString(fileContents));
        if (doc.Root is null)
        {
            return results;
        }
        Walk(doc.Root, ancestors: Array.Empty<string>(), results);
        return results;
    }

    private static void Walk(XElement element, string[] ancestors, List<GameConfigEntry> results)
    {
        string localName = element.Name.LocalName;
        string ownPath = ancestors.Length == 0 ? localName : string.Join('.', ancestors) + "." + localName;

        // Attributes: Section = element's own path, Key = "@" + attr name.
        foreach (XAttribute attr in element.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
            {
                continue;
            }
            string rawLine = ownPath + " @" + attr.Name.LocalName + "=" + attr.Value;
            results.Add(new GameConfigEntry(
                Section: ownPath,
                Key: "@" + attr.Name.LocalName,
                Value: attr.Value,
                RawLine: rawLine));
        }

        bool hasChildElements = element.HasElements;
        if (!hasChildElements)
        {
            // Scalar element: Section = ancestor path, Key = local name.
            string? section = ancestors.Length == 0 ? null : string.Join('.', ancestors);
            string value = element.Value;
            string rawLine = ownPath + "=" + value;
            results.Add(new GameConfigEntry(section, localName, value, rawLine));
            return;
        }

        // Recurse — extend ancestors with this element's name.
        string[] childAncestors = new string[ancestors.Length + 1];
        Array.Copy(ancestors, childAncestors, ancestors.Length);
        childAncestors[^1] = localName;
        foreach (XElement child in element.Elements())
        {
            Walk(child, childAncestors, results);
        }
    }
}
