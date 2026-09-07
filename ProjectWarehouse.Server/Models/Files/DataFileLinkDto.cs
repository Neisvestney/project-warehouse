using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Files;

/// <summary>
/// One element of an entity's file attachment list — shared shape for every 1:N attachment point
/// (receipts, write-offs, orders, stocktakes, …). <c>IHasIdentity</c> matters here: the changelog's
/// compare logic matches collection elements by Id rather than by position, so reordering files does
/// not read as a full rewrite.
/// </summary>
public class DataFileLinkDto : IHasIdentity
{
    public Guid Id { get; init; }
    public DataFileDto File { get; init; } = null!;
    public int Order { get; init; }
}
