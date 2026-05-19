using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class InboundOrder : IHasIdentity
{
    public Guid Id { get; set; }
    public int Number { get; set; }

    public InboundOrderStatus Status { get; set; }
    
    // Additional Data
    public string? Title {get; set; }
    public DateTime PlannedStartDateTime { get; set; }
    public string? Notes {get; set; }
    
    // Draft Status
    public ICollection<InboundOrderDraftItemsGroup> DraftItemsGroups { get; set; } = [];
    
    // Processing Status
    public ICollection<ApplicationUser> AssignedUsers {get; set;} = [];
    
    // Processing and Finished Status
    public ICollection<InboundOrderDeclaredItemsGroup> DeclaredItemsGroups {get; set;} = [];
    public ICollection<InboundOrderProcessedItemsGroup> ProcessedItemsGroups {get; set;} = [];

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    [Projectable] public string SearchString => "#" + Number + " " + (Title ?? "");
}

public enum InboundOrderStatus
{
    Draft,
    Processing,
    Finished,
}
