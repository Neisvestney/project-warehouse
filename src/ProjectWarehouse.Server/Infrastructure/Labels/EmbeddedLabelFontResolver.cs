using System.Reflection;
using PdfSharp.Fonts;

namespace ProjectWarehouse.Server.Infrastructure.Labels;

/// <summary>
/// Serves one embedded face for every request. PDFsharp has no fonts of its own and the Linux container
/// has no system fonts, so without this every label would print tofu boxes — and that would be
/// discovered at the warehouse, not in CI.
/// </summary>
public class EmbeddedLabelFontResolver : IFontResolver
{
    /// <summary>The family name to pass to <c>new XFont(...)</c>; the resolver ignores anything else.</summary>
    public const string FamilyName = "LabelFont";

    private const string FaceName = "LabelFont#Regular";

    private readonly byte[]? _font;

    public EmbeddedLabelFontResolver(string resourceName, ILogger<EmbeddedLabelFontResolver> logger)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            // Not fatal on purpose: a developer without the font file must still be able to run the app.
            // Label generation then fails loudly instead of quietly printing boxes.
            logger.LogError("Label font resource {ResourceName} is missing; labels cannot be generated",
                resourceName);
            return;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        _font = buffer.ToArray();
    }

    public bool IsAvailable => _font is not null;

    // one face — bold and italic requests collapse onto it rather than failing
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        _font is null ? null : new FontResolverInfo(FaceName);

    public byte[]? GetFont(string faceName) => _font;
}
