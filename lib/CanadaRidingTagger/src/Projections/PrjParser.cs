using System.Globalization;

namespace CanadaRidingTagger.Projections;

/// <summary>
/// A node of a parsed WKT tree: either a named element (an identifier with
/// zero or more children) or a literal value (a quoted string or a
/// number).
/// </summary>
internal sealed class WktNode
{
    /// <summary>Element name (set for named elements only).</summary>
    public string? Name { get; init; }

    /// <summary>Quoted string value (set for string literals only).</summary>
    public string? Value { get; init; }

    /// <summary>Number value (set for numeric literals only).</summary>
    public double? Number { get; init; }

    /// <summary>Child elements (empty for literals).</summary>
    public WktNode[] Children { get; init; } = [];
}

/// <summary>
/// Parses the WKT subset written by ESRI in <c>.prj</c> files:
/// <c>NAME[ "string" | NAME[ ... ] | number ]</c>, comma-separated.
/// Throws <see cref="FormatException"/> on malformed input.
/// </summary>
internal static class PrjParser
{
    /// <summary>
    /// Parse a complete WKT document into a tree. The root is a named
    /// element (for example <c>PROJCS</c> or <c>GEOGCS</c>).
    /// </summary>
    public static WktNode Parse(string wkt)
    {
        int pos = 0;
        WktNode root = ParseElement(wkt, ref pos);
        SkipWs(wkt, ref pos);
        if (pos != wkt.Length)
        {
            throw new FormatException($"Trailing data at position {pos}");
        }
        return root;
    }

    private static WktNode ParseElement(string s, ref int pos)
    {
        SkipWs(s, ref pos);
        char c = s[pos];
        if (c == '"')
        {
            return new WktNode { Value = ParseQuoted(s, ref pos) };
        }
        if (c == '-' || (c >= '0' && c <= '9'))
        {
            int numStart = pos;
            pos++;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.'))
            {
                pos++;
            }
            return new WktNode
            {
                Number = double.Parse(s[numStart..pos], CultureInfo.InvariantCulture),
            };
        }

        // Identifier, optionally followed by a child list.
        int start = pos;
        while (pos < s.Length && (char.IsLetterOrDigit(s[pos]) || s[pos] == '_'))
        {
            pos++;
        }
        string name = s[start..pos];
        SkipWs(s, ref pos);
        WktNode[] children = [];
        if (pos < s.Length && s[pos] == '[')
        {
            children = ParseList(s, ref pos);
        }
        return new WktNode { Name = name, Children = children };
    }

    // s[pos] == '['. Advances past the matching ']'.
    private static WktNode[] ParseList(string s, ref int pos)
    {
        pos++;
        var list = new List<WktNode>();
        for (;;)
        {
            SkipWs(s, ref pos);
            if (s[pos] == ']')
            {
                pos++;
                return list.ToArray();
            }
            list.Add(ParseElement(s, ref pos));
            SkipWs(s, ref pos);
            if (s[pos] == ']')
            {
                pos++;
                return list.ToArray();
            }
            if (s[pos] != ',')
            {
                throw new FormatException(
                    $"Expected ',' or ']' at position {pos}");
            }
            pos++;
        }
    }

    // s[pos] == '"'. No escapes in ESRI WKT.
    private static string ParseQuoted(string s, ref int pos)
    {
        pos++;
        int start = pos;
        while (s[pos] != '"')
        {
            pos++;
        }
        string text = s[start..pos];
        pos++;
        return text;
    }

    private static void SkipWs(string s, ref int pos)
    {
        while (pos < s.Length && (s[pos] == ' ' || s[pos] == '\n' || s[pos] == '\r'))
        {
            pos++;
        }
    }
}
