using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ProjectWarehouse.Server.Infrastructure.Labels;

/// <summary>
/// Reads the printed text off a label page so a page can be matched to the posting it belongs to.
/// </summary>
/// <remarks>
/// Ozon embeds a subset font and addresses its glyphs by id, so the barcode digits never appear as
/// literal bytes in the content stream — they have to be mapped back through the font's ToUnicode CMap.
/// Only enough of the PDF text model is implemented to recover a barcode: strings are concatenated in
/// stream order with no separator, because Ozon splits one barcode across several show operators.
/// Anything unreadable comes back as null, which the caller treats as "cannot tell", never as an error.
/// </remarks>
public static partial class LabelTextReader
{
    public static string? ReadText(byte[] pdf)
    {
        try
        {
            using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
            if (document.PageCount == 0)
                return null;

            var page = document.Pages[0];
            var map = BuildUnicodeMap(page);

            var streams = ReadContent(page);
            if (streams.Count == 0)
                return null;

            // One content stream is one writer: the marketplace's own label is one, anything stamped on
            // afterwards is another. Keeping them apart stops a stamped article from reading as part of
            // the barcode next to it. Inside a stream nothing is inserted — the marketplace splits one
            // barcode across separate text objects, and those have to close back up.
            return string.Join('\n', streams.Select(s => Decode(s, map)));
        }
        catch (Exception)
        {
            // not knowing is not an error here — the caller decides what an unreadable page means
            return null;
        }
    }

    /// <summary>True when <paramref name="pdf"/> prints <paramref name="barcode"/> somewhere on its first page.</summary>
    public static bool Contains(byte[] pdf, string barcode) =>
        ContainsBarcode(ReadText(pdf), barcode);

    /// <summary>
    /// True when <paramref name="text"/> carries <paramref name="barcode"/> and not merely some longer
    /// code that contains it.
    /// </summary>
    /// <remarks>
    /// A plain substring test would accept one barcode inside a longer one, which is how a page ends up
    /// on the wrong box, so a match with a digit against either end does not count. Only digits: the
    /// text of a page is the show operators concatenated with no separator, so whatever is printed next
    /// to the barcode ends up glued to it, and rejecting a neighbouring letter would reject the match on
    /// every page that carries anything else at all.
    /// </remarks>
    public static bool ContainsBarcode(string? text, string? barcode)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(barcode))
            return false;

        for (var at = 0; at + barcode.Length <= text.Length;)
        {
            var found = text.IndexOf(barcode, at, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
                return false;

            var beforeOk = found == 0 || !char.IsDigit(text[found - 1]);
            var after = found + barcode.Length;
            var afterOk = after == text.Length || !char.IsDigit(text[after]);

            if (beforeOk && afterOk)
                return true;

            at = found + 1;
        }

        return false;
    }

    private static List<string> ReadContent(PdfPage page)
    {
        var streams = new List<string>();
        foreach (var content in page.Contents)
            if (content?.Stream?.UnfilteredValue is { Length: > 0 } bytes)
                streams.Add(Encoding.Latin1.GetString(bytes));

        return streams;
    }

    /// <summary>Glyph code to text, merged across every font the page declares.</summary>
    private static Dictionary<int, string> BuildUnicodeMap(PdfPage page)
    {
        var map = new Dictionary<int, string>();
        var fonts = page.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/Font");
        if (fonts is null)
            return map;

        foreach (var key in fonts.Elements.Keys)
        {
            var cmap = fonts.Elements.GetDictionary(key)?.Elements.GetDictionary("/ToUnicode");
            if (cmap?.Stream?.UnfilteredValue is { Length: > 0 } bytes)
                ParseCMap(Encoding.Latin1.GetString(bytes), map);
        }

        return map;
    }

    private static void ParseCMap(string cmap, Dictionary<int, string> map)
    {
        foreach (Match block in BfCharBlock().Matches(cmap))
            foreach (Match pair in HexPair().Matches(block.Groups[1].Value))
                if (TryHex(pair.Groups[1].Value, out var code) && FromUtf16Be(pair.Groups[2].Value) is { } text)
                    map[code] = text;

        foreach (Match block in BfRangeBlock().Matches(cmap))
            foreach (Match triple in HexTriple().Matches(block.Groups[1].Value))
            {
                if (!TryHex(triple.Groups[1].Value, out var lo)
                    || !TryHex(triple.Groups[2].Value, out var hi)
                    || !TryHex(triple.Groups[3].Value, out var start))
                    continue;

                // a range maps consecutive codes onto consecutive code points
                for (var code = lo; code <= hi && code - lo < 0xFFFF; code++)
                    map[code] = char.ConvertFromUtf32(start + (code - lo));
            }
    }

    private static string Decode(string content, IReadOnlyDictionary<int, string> map)
    {
        var text = new StringBuilder();

        foreach (Match match in ShowOperand().Matches(content))
        {
            if (match.Groups["hex"].Success)
                AppendHex(match.Groups["hex"].Value, map, text);
            else
                AppendLiteral(match.Groups["lit"].Value, map, text);
        }

        return text.ToString();
    }

    private static void AppendHex(string hex, IReadOnlyDictionary<int, string> map, StringBuilder text)
    {
        var digits = WhitespaceRun().Replace(hex, string.Empty);
        if (digits.Length == 0 || digits.Length % 4 != 0)
            return;

        // two bytes per glyph: Ozon's label fonts are Type0 with an Identity encoding
        for (var i = 0; i < digits.Length; i += 4)
            if (TryHex(digits.Substring(i, 4), out var code) && map.TryGetValue(code, out var glyph))
                text.Append(glyph);
    }

    private static void AppendLiteral(string literal, IReadOnlyDictionary<int, string> map, StringBuilder text)
    {
        // a simple font addresses glyphs by single byte, and without a CMap the byte is already the character
        foreach (var c in Unescape(literal))
            text.Append(map.TryGetValue(c, out var glyph) ? glyph : c);
    }

    private static string Unescape(string literal)
    {
        var buffer = new StringBuilder(literal.Length);
        for (var i = 0; i < literal.Length; i++)
        {
            if (literal[i] != '\\' || i + 1 == literal.Length)
            {
                buffer.Append(literal[i]);
                continue;
            }

            var next = literal[++i];
            buffer.Append(next switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'b' => '\b',
                'f' => '\f',
                _ => next,
            });
        }

        return buffer.ToString();
    }

    private static bool TryHex(string value, out int result) =>
        int.TryParse(value, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out result);

    private static string? FromUtf16Be(string hex)
    {
        if (hex.Length == 0 || hex.Length % 4 != 0)
            return null;

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            if (TryHex(hex.Substring(i * 2, 2), out var b))
                bytes[i] = (byte)b;
            else
                return null;

        return Encoding.BigEndianUnicode.GetString(bytes);
    }

    [GeneratedRegex(@"beginbfchar(.*?)endbfchar", RegexOptions.Singleline)]
    private static partial Regex BfCharBlock();

    [GeneratedRegex(@"beginbfrange(.*?)endbfrange", RegexOptions.Singleline)]
    private static partial Regex BfRangeBlock();

    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>")]
    private static partial Regex HexPair();

    [GeneratedRegex(@"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>")]
    private static partial Regex HexTriple();

    [GeneratedRegex(@"<(?<hex>[0-9A-Fa-f\s]*)>|\((?<lit>(?:[^()\\]|\\.)*)\)")]
    private static partial Regex ShowOperand();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
