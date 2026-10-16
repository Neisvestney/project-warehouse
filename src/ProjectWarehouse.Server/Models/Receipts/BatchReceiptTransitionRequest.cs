using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Receipts;

public class BatchReceiptTransitionRequest
{
    public IReadOnlyList<Guid> Ids { get; init; } = [];
    [JsonRequired] public ReceiptTransition Transition { get; init; }
}
