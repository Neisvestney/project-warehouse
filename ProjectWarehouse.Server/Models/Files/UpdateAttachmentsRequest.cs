namespace ProjectWarehouse.Server.Models.Files;

/// <summary>Body of the dedicated attachments endpoint, shared by every 1:N attachment point.</summary>
public class UpdateAttachmentsRequest
{
    public IReadOnlyList<DataFileLinkRequest> Attachments { get; init; } = [];
}
