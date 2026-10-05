using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Organizations;

public class SaveOrganizationRequest
{
    [JsonRequired] [Required] [MaxLength(256)]
    public string Name { get; init; } = null!;

    [JsonRequired] [Required]
    public string Inn { get; init; } = null!;

    [MaxLength(512)] public string? LegalName { get; init; }
    [MaxLength(9)] public string? Kpp { get; init; }
    [MaxLength(15)] public string? Ogrn { get; init; }
    [MaxLength(64)] public string? OwnershipForm { get; init; }
}

public class SetAccountOrganizationRequest
{
    /// <summary>Null hands the account back to automatic linking by its INN.</summary>
    public Guid? OrganizationId { get; init; }
}
