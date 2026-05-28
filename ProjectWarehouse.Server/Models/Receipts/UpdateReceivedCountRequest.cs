using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Receipts;

public class UpdateReceivedCountRequest
{
    [Range(0, int.MaxValue)]
    public int? ReceivedCount { get; init; }
}
