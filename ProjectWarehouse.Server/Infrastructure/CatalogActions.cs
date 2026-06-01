namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries produced by CatalogService.</summary>
public static class CatalogActions
{
    public const string BundleSync              = "catalog.bundle_sync";
    public const string ComponentArticleChanged = "catalog.component_article_changed";
}
