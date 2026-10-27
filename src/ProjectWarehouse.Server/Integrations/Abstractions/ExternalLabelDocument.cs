using ProjectWarehouse.Server.Infrastructure.Marketplaces;

namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// One label document covering the whole requested batch, in request order. The provider does not
/// slice it per posting: mapping pages to postings is a PDF concern and belongs to the label service.
/// </summary>
/// <remarks>
/// <c>IsReady = false</c> is a normal answer, not a failure — the marketplace has not produced the
/// labels yet, and it says so about the batch as a whole. A marketplace that can fail part of a batch
/// names those postings in <see cref="Unprinted"/> instead, and the document still carries the pages
/// of the rest. A marketplace that answers per posting (Wildberries stickers) fills <see cref="Pages"/>
/// instead of <see cref="Content"/>: each page is already tied to its posting, so nothing is matched by
/// the printed text, and a posting with no page is one the marketplace did not print.
/// </remarks>
public record ExternalLabelDocument(
    bool IsReady,
    IReadOnlyList<string> PostingNumbers,
    string? ContentType,
    byte[]? Content,
    IReadOnlyList<ExternalLabelFailure>? Unprinted = null,
    IReadOnlyList<ExternalLabelPage>? Pages = null,
    IReadOnlyList<ExternalLabelFailure>? Unreadable = null)
{
    /// <summary>Postings of the batch the marketplace produced no page for. Empty when all of them printed.</summary>
    public IReadOnlyList<ExternalLabelFailure> Unprinted { get; init; } = Unprinted ?? [];

    /// <summary>Postings whose page came back but could not be decoded, with the reason.</summary>
    public IReadOnlyList<ExternalLabelFailure> Unreadable { get; init; } = Unreadable ?? [];
}

/// <summary>One posting the marketplace declined to print, as it worded the reason.</summary>
public record ExternalLabelFailure(string PostingNumber, string? Message);

/// <summary>
/// A single-page PDF label of one posting, in the layout <paramref name="Kind"/> names, with the number
/// printed on it when the marketplace states one.
/// </summary>
public record ExternalLabelPage(string PostingNumber, byte[] Content, LabelKind Kind, string? Barcode = null);
