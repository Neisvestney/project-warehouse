namespace ProjectWarehouse.Server.Models.Stocktakes;

/// <summary>What finishing the document will do to a single position.</summary>
public enum StocktakeDifferenceResolution
{
    NoChange = 0,
    Surplus = 1,
    Shortage = 2,

    /// <summary>A serial found in a cell other than the one it is booked in — will be moved.</summary>
    Relocation = 3,

    /// <summary>A serial unknown to the system — will be created in the counted cell.</summary>
    CreateUnit = 4,

    /// <summary>A serial the count did not find — will be detached from its cell.</summary>
    DetachUnit = 5,

    /// <summary>A previously detached serial found again — will be reattached to the counted cell.</summary>
    ReattachUnit = 6,
}
