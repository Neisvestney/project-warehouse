namespace ProjectWarehouse.Server.Infrastructure.Files;

/// <summary>Join entity between an owner and a file, e.g. <c>CatalogItemImage</c>.</summary>
public interface IDataFileLink : IHasIdentity
{
    Guid DataFileId { get; set; }
    int Order { get; set; }
}

/// <summary>One element of a file list in an update request.</summary>
public interface IDataFileLinkRequest
{
    /// <summary>Null for a link that does not exist yet.</summary>
    Guid? Id { get; }

    Guid FileId { get; }
    int Order { get; }
}
