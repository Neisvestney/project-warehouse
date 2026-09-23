namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// One label document covering the whole requested batch, in request order. The provider does not
/// slice it per posting: mapping pages to postings is a PDF concern and belongs to the label service.
/// </summary>
/// <remarks>
/// <c>IsReady = false</c> is a normal answer, not a failure — the marketplace has not produced the
/// labels yet, and it says so about the batch as a whole. A marketplace that can fail part of a batch
/// names those postings in <see cref="Unprinted"/> instead, and the document still carries the pages
/// of the rest. <c>ContentType</c> is carried because marketplaces differ: Ozon returns PDF,
/// Wildberries a raster or SVG sticker, and the consumer decides how to place the content on a page.
/// </remarks>
public record ExternalLabelDocument(
    bool IsReady,
    IReadOnlyList<string> PostingNumbers,
    string? ContentType,
    byte[]? Content,
    IReadOnlyList<ExternalLabelFailure>? Unprinted = null)
{
    /// <summary>Postings of the batch the marketplace produced no page for. Empty when all of them printed.</summary>
    public IReadOnlyList<ExternalLabelFailure> Unprinted { get; init; } = Unprinted ?? [];
}

/// <summary>One posting the marketplace declined to print, as it worded the reason.</summary>
public record ExternalLabelFailure(string PostingNumber, string? Message);
