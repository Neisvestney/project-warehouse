using System.Globalization;
using Microsoft.Extensions.Options;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;

namespace ProjectWarehouse.Server.Infrastructure.Labels;

/// <summary>
/// PDF surgery for marketplace labels: slice a batch into per-posting documents, stamp WMS articles
/// along the left edge, merge the result for printing. No database, no HTTP.
/// </summary>
public class LabelPdfComposer(IOptions<MarketplacesOptions> options)
{
    private readonly LabelsOptions _options = options.Value.Labels;

    public static int PageCount(byte[] pdf)
    {
        // Import rather than InformationOnly — PDFsharp 6 marks the latter as not implemented
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    /// <summary>One single-page document per page, in order.</summary>
    public static IReadOnlyList<byte[]> SplitPages(byte[] pdf)
    {
        // Import, not Modify: this document is only a source of pages
        using var source = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Import);

        var pages = new List<byte[]>(source.PageCount);
        for (var i = 0; i < source.PageCount; i++)
        {
            using var single = new PdfDocument();
            single.AddPage(source.Pages[i]);
            pages.Add(Save(single));
        }

        return pages;
    }

    public static byte[] Merge(IReadOnlyList<byte[]> documents)
    {
        using var merged = new PdfDocument();
        foreach (var document in documents)
        {
            using var source = PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Import);
            foreach (var page in source.Pages)
                merged.AddPage(page);
        }

        return Save(merged);
    }

    /// <summary>
    /// Writes the article lines along the top edge of every page in the document.
    /// </summary>
    /// <remarks>
    /// No rotation: Ozon already hands the label over rotated, so the page arrives in the orientation it
    /// is printed in and the text only has to follow it.
    /// </remarks>
    public byte[] Overlay(byte[] pdf, IReadOnlyList<LabelArticle> articles)
    {
        var lines = BuildLines(articles);
        if (lines.Count == 0)
            return pdf;

        // Modify, because this document is the one being edited
        using var document = PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify);
        var font = new XFont(EmbeddedLabelFontResolver.FamilyName, _options.FontSize);
        var lineHeight = _options.FontSize * 1.25;

        foreach (var page in document.Pages)
        {
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            var width = lines.Max(l => gfx.MeasureString(l, font).Width);
            // Ozon prints up to the edge too; a plate keeps the articles readable over it
            gfx.DrawRectangle(XBrushes.White,
                _options.Margin - 2, _options.Margin - 2, width + 4, lines.Count * lineHeight + 4);

            for (var i = 0; i < lines.Count; i++)
                gfx.DrawString(lines[i], font, XBrushes.Black,
                    _options.Margin, _options.Margin + i * lineHeight, XStringFormats.TopLeft);
        }

        return Save(document);
    }

    /// <summary>Articles in posting order, quantities appended, the tail collapsed into "+N".</summary>
    public IReadOnlyList<string> BuildLines(IReadOnlyList<LabelArticle> articles)
    {
        var lines = articles
            .Take(_options.MaxArticlesOnLabel)
            .Select(a => a.Quantity > 1
                ? $"{a.Article} ×{a.Quantity.ToString(CultureInfo.InvariantCulture)}"
                : a.Article)
            .ToList();

        var remaining = articles.Count - lines.Count;
        if (remaining > 0)
            lines.Add($"+{remaining.ToString(CultureInfo.InvariantCulture)}");

        return lines;
    }

    private static byte[] Save(PdfDocument document)
    {
        using var buffer = new MemoryStream();
        document.Save(buffer, closeStream: false);
        return buffer.ToArray();
    }
}

public record LabelArticle(string Article, int Quantity);
