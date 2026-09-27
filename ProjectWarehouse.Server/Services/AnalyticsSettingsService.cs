using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

public class AnalyticsSettingsService(ApplicationDbContext db) : IAnalyticsSettingsService
{
    public async Task<AnalyticsOptions> GetOptionsAsync(CancellationToken ct = default) =>
        AnalyticsCalculator.Resolve(await db.AnalyticsSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == AnalyticsSettings.SingletonId, ct));

    public async Task<AnalyticsSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await LoadSettingsAsync(ct);
        if (settings is null) return ToDto(new AnalyticsSettings { Id = AnalyticsSettings.SingletonId }, 0, null);

        return ToDto(settings, db.Entry(settings).Property<uint>("Version").CurrentValue, settings.UpdatedBy?.FullName);
    }

    public async Task<(AnalyticsSettingsDto Before, AnalyticsSettingsDto After)> UpdateSettingsAsync(
        UpdateAnalyticsSettingsRequest request, Guid? actorId, CancellationToken ct = default)
    {
        ValidateSettings(request);

        var settings = await LoadSettingsAsync(ct);
        if (settings is null)
        {
            // The row is seeded by the migration; this only restores it after a manual delete
            settings = new AnalyticsSettings { Id = AnalyticsSettings.SingletonId };
            db.AnalyticsSettings.Add(settings);
        }
        else
        {
            db.Entry(settings).Property<uint>("Version").OriginalValue = request.Version!.Value;
        }

        var before = ToDto(settings, request.Version!.Value, settings.UpdatedBy?.FullName);

        settings.AbcBoundaryA = request.AbcBoundaryA;
        settings.AbcBoundaryB = request.AbcBoundaryB;
        settings.XyzBoundaryX = request.XyzBoundaryX;
        settings.XyzBoundaryY = request.XyzBoundaryY;
        settings.XyzStep = request.XyzStep;
        settings.XyzMinIntervals = request.XyzMinIntervals;
        settings.PayoutRatioWindowDays = request.PayoutRatioWindowDays;
        settings.PayoutAgeBoundaries = request.PayoutAgeBoundaries;
        settings.PayoutOverdueDays = request.PayoutOverdueDays;
        settings.ReturnsMaturityDays = request.ReturnsMaturityDays;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedById = actorId;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException e)
        {
            throw new AnalyticsSettingsConflictException(e);
        }

        var updatedBy = actorId is { } id ? await db.Users.FindAsync([id], ct) : null;

        return (before,
            ToDto(settings, db.Entry(settings).Property<uint>("Version").CurrentValue, updatedBy?.FullName));
    }

    /// <summary>With the author, for the settings form and its changelog snapshot; reports read the row bare.</summary>
    private Task<AnalyticsSettings?> LoadSettingsAsync(CancellationToken ct) =>
        db.AnalyticsSettings
            .Include(s => s.UpdatedBy)
            .FirstOrDefaultAsync(s => s.Id == AnalyticsSettings.SingletonId, ct);

    /// <summary>
    /// Range violations are caught by <c>[Range]</c> before this runs. Pair order is checked on the values
    /// that will apply, so a lone A above the default B conflicts as surely as an explicit pair would.
    /// </summary>
    private static void ValidateSettings(UpdateAnalyticsSettingsRequest request)
    {
        foreach (var (field, value) in new[]
                 {
                     ("abcBoundaryA", request.AbcBoundaryA), ("abcBoundaryB", request.AbcBoundaryB),
                     ("xyzBoundaryX", request.XyzBoundaryX), ("xyzBoundaryY", request.XyzBoundaryY),
                 })
        {
            if (value is { } v && decimal.Round(v, 1) != v)
                throw new ValidationException(field, ErrorCode.InvalidValue,
                    "A boundary is a percentage with at most one decimal place.");
        }

        var abcA = request.AbcBoundaryA ?? AnalyticsCalculator.DefaultAbcBoundaryA;
        var abcB = request.AbcBoundaryB ?? AnalyticsCalculator.DefaultAbcBoundaryB;
        if (abcB <= abcA)
            throw new ValidationException("abcBoundaryB", ErrorCode.InvalidValue,
                "Boundary B must be greater than boundary A.");

        var xyzX = request.XyzBoundaryX ?? AnalyticsCalculator.DefaultXyzBoundaryX;
        var xyzY = request.XyzBoundaryY ?? AnalyticsCalculator.DefaultXyzBoundaryY;
        if (xyzY <= xyzX)
            throw new ValidationException("xyzBoundaryY", ErrorCode.InvalidValue,
                "Boundary Y must be greater than boundary X.");

        if (request.PayoutAgeBoundaries is { } ages)
        {
            if (ages.Length is 0 or > AnalyticsCalculator.MaxPayoutAgeBoundaryCount)
                throw new ValidationException("payoutAgeBoundaries", ErrorCode.ValidationError,
                    $"Between 1 and {AnalyticsCalculator.MaxPayoutAgeBoundaryCount} age boundaries are allowed.");

            for (var i = 0; i < ages.Length; i++)
            {
                if (ages[i] is < AnalyticsCalculator.MinPayoutAgeBoundary or > AnalyticsCalculator.MaxPayoutAgeBoundary)
                    throw new ValidationException($"payoutAgeBoundaries[{i}]", ErrorCode.ValidationError,
                        $"An age boundary must be between {AnalyticsCalculator.MinPayoutAgeBoundary} and {AnalyticsCalculator.MaxPayoutAgeBoundary} days.");
            }

            for (var i = 1; i < ages.Length; i++)
            {
                if (ages[i] <= ages[i - 1])
                    throw new ValidationException("payoutAgeBoundaries", ErrorCode.InvalidValue,
                        "Age boundaries must be strictly ascending.");
            }
        }
    }

    private static AnalyticsSettingsDto ToDto(AnalyticsSettings settings, uint version, string? updatedByName) => new()
    {
        Id = settings.Id,
        Saved = new AnalyticsSettingsValuesDto
        {
            AbcBoundaryA = settings.AbcBoundaryA,
            AbcBoundaryB = settings.AbcBoundaryB,
            XyzBoundaryX = settings.XyzBoundaryX,
            XyzBoundaryY = settings.XyzBoundaryY,
            XyzStep = settings.XyzStep,
            XyzMinIntervals = settings.XyzMinIntervals,
            PayoutRatioWindowDays = settings.PayoutRatioWindowDays,
            PayoutAgeBoundaries = settings.PayoutAgeBoundaries,
            PayoutOverdueDays = settings.PayoutOverdueDays,
            ReturnsMaturityDays = settings.ReturnsMaturityDays,
        },
        Defaults = AnalyticsCalculator.Defaults,
        Effective = AnalyticsCalculator.Resolve(settings),
        Version = version,
        UpdatedAt = settings.UpdatedAt,
        UpdatedByName = updatedByName,
    };
}
