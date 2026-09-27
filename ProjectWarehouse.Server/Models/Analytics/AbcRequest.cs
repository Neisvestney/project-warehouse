using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Analytics;

public class AbcRequest : AbcFilterRequest
{
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 25;
}
