namespace ProjectWarehouse.Server.Models.Integrations;

/// <summary>Null clears the mapping.</summary>
public class SetWarehouseMappingRequest
{
    public Guid? WarehouseId { get; init; }
}

/// <summary>Null clears the mapping.</summary>
public class SetCardMappingRequest
{
    public Guid? CatalogItemId { get; init; }
    public bool IsMarkedArchived { get; init; }
}

public class AutoMapResponse
{
    public int Mapped { get; init; }
    public int Remaining { get; init; }
}
