using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Writeoffs;

public class CreateWriteoffTagRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = null!;
}
