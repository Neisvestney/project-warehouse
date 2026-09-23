namespace ProjectWarehouse.Server.Integrations.Ozon;

/// <summary>
/// "Labels aren't printed yet" is a normal answer, but Ozon delivers it inconsistently — sometimes as a
/// 200 with a JSON body, sometimes as a 400/409. Both paths funnel through here so the two callers
/// cannot drift apart.
/// </summary>
internal static class OzonLabelHeuristics
{
    private static readonly string[] NotReadyMarkers =
    [
        "aren't ready",
        "are not ready",
        "not ready",
        "не готов",
        // how /v3/posting/fbs/package-label/create rejects a posting it will not print a label for
        "NO_POSTINGS_FOR_BATCH_DOWNLOAD",
    ];

    public static bool LooksNotReady(string? body) =>
        !string.IsNullOrWhiteSpace(body)
        && NotReadyMarkers.Any(marker => body.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
