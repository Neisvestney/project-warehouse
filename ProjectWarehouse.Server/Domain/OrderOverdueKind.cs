namespace ProjectWarehouse.Server.Domain;

/// <summary>Which step of an order past its planned shipment date is late.</summary>
public enum OrderOverdueKind
{
    /// <summary>Not assembled yet: draft, confirmed or in assembly.</summary>
    Assembly = 0,

    /// <summary>Assembled but not shipped.</summary>
    Shipment = 1,
}
