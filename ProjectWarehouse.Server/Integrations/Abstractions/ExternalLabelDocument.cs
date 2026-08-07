namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// One label document covering the whole requested batch, in request order. The provider does not
/// slice it per posting: mapping pages to postings is a PDF concern and belongs to the label service.
/// </summary>
/// <remarks>
/// <c>IsReady = false</c> is a normal answer, not a failure — the marketplace has not produced the
/// labels yet. Ozon asks for a 45–60 second wait after packing and replies "The next postings aren't
/// ready". <c>ContentType</c> is carried because marketplaces differ: Ozon returns PDF, Wildberries a
/// raster or SVG sticker, and the consumer decides how to place the content on a page.
/// </remarks>
public record ExternalLabelDocument(
    bool IsReady,
    IReadOnlyList<string> PostingNumbers,
    string? ContentType,
    byte[]? Content);
