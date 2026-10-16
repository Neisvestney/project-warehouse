namespace ProjectWarehouse.Server.Models.Catalog;

/// <summary>Aggregates of the whole filtered catalog list.</summary>
public class CatalogListMetaDto
{
    /// <summary>
    /// Items whose name, article or label text has leading, trailing, doubled or irregular whitespace, or whose
    /// barcode or GTIN contains any whitespace; a product group counts its children too. Ignores the
    /// <c>whitespaceIssues</c> filter so the counter stays visible while it is applied.
    /// </summary>
    public int WhitespaceIssueCount { get; init; }
}
