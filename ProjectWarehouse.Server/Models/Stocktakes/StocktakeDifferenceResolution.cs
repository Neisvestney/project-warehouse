namespace ProjectWarehouse.Server.Models.Stocktakes;

/// <summary>What finishing the document will do to a single position.</summary>
public enum StocktakeDifferenceResolution
{
    NoChange,
    Surplus,
    Shortage,

    /// <summary>A serial found in a cell other than the one it is booked in — will be moved.</summary>
    Relocation,

    /// <summary>A serial unknown to the system — will be created in the counted cell.</summary>
    CreateUnit,

    /// <summary>A serial the count did not find — will be detached from its cell.</summary>
    DetachUnit,

    /// <summary>A previously detached serial found again — will be reattached to the counted cell.</summary>
    ReattachUnit,
}
