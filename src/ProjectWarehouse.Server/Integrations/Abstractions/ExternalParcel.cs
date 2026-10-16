namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>One place of a posting that leaves with a label of its own. Empty <see cref="Items"/> — the whole posting.</summary>
public record ExternalParcel(string? ExternalId, string? Barcode, IReadOnlyList<ExternalParcelItem> Items);

/// <summary>
/// What a parcel holds: whole units (<see cref="Quantity"/>) or part <see cref="PartIndex"/> of
/// <see cref="PartCount"/> of a single unit. <see cref="ItemIndex"/> points into <see cref="ExternalPosting.Items"/>.
/// </summary>
public record ExternalParcelItem(int ItemIndex, int? Quantity, int? PartIndex, int? PartCount);
