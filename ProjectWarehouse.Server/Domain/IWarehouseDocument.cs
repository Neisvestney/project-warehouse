using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>A numbered document bound to one warehouse — receipt, write-off, stocktake.</summary>
public interface IWarehouseDocument : IHasIdentity
{
    int Number { get; }
    Guid WarehouseId { get; }
}

public interface ITaggedWarehouseDocument<TTag> : IWarehouseDocument where TTag : Tag
{
    ICollection<TTag> Tags { get; }
}
