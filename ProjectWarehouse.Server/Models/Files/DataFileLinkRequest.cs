using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Infrastructure.Files;

namespace ProjectWarehouse.Server.Models.Files;

/// <summary>Request-side counterpart of <see cref="DataFileLinkDto"/>, shared by every attachment point.</summary>
public class DataFileLinkRequest : IDataFileLinkRequest
{
    /// <summary>Null for a link not yet attached to the entity.</summary>
    public Guid? Id { get; init; }

    [Required]
    public Guid FileId { get; init; }

    public int Order { get; init; }
}
