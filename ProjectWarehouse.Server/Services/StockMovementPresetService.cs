using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Models.Statistics;

namespace ProjectWarehouse.Server.Services;

public class StockMovementPresetService(ApplicationDbContext db) : IStockMovementPresetService
{
    /// <summary>
    /// Every write to the preset table is check-then-act over the whole table — "is there a default?",
    /// "is this the last one?" — and the partial unique index on <c>IsDefault</c> is checked per statement,
    /// so two transactions flipping the default at once collide on it. A transaction-scoped advisory lock
    /// serialises them instead; the table is tiny and written by hand, so the contention costs nothing.
    /// Key derived like <see cref="Integrations.Sync.PostgresAdvisoryLock"/>'s, to share the key space safely.
    /// </summary>
    private static readonly long WriteLockKey =
        BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes("stock-movement-presets:write")), 0);

    /// <summary>
    /// The preset plus the two values that are not columns of its own: the shadow concurrency token and
    /// the editor's name. <c>Metrics</c> is a jsonb document, so it comes back whole and is shaped in
    /// memory — a projection into the DTO would have to be translated into SQL and cannot be.
    /// </summary>
    private sealed record PresetRow(StockMovementReportPreset Preset, uint Version, string? UpdatedByName);

    public async Task<IReadOnlyList<StockMovementReportPresetDto>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await Rows(db.StockMovementReportPresets
                .OrderByDescending(p => p.IsDefault)
                .ThenBy(p => p.Name))
            .ToListAsync(ct);

        return rows.Select(ToDto).ToList();
    }

    public async Task<StockMovementReportPresetDto> CreateAsync(
        SaveStockMovementReportPresetRequest request,
        Guid? actorId,
        CancellationToken ct = default)
    {
        var name = Validate(request);

        return await db.Database.ExecuteInTransactionAsync("statistics.preset.create", async () =>
        {
            await TakeWriteLockAsync(ct);

            var now = DateTime.UtcNow;
            var preset = new StockMovementReportPreset
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = now,
                UpdatedAt = now,
                UpdatedById = actorId,
                Metrics = ToDomain(request.Metrics),
            };

            db.StockMovementReportPresets.Add(preset);
            await SaveAsync(ct);

            // The very first preset has to be the default, or the page opens with no columns at all. The
            // insert and the flag share one transaction: a preset committed without the default it was
            // promised would leave the table with no default, and the seeder only refills an empty table.
            var makeDefault =
                request.IsDefault || !await db.StockMovementReportPresets.AnyAsync(p => p.IsDefault, ct);
            if (makeDefault)
                await SetDefaultAsync(preset, ct);

            return await ProjectAsync(preset.Id, ct);
        }, ct);
    }

    public async Task<(StockMovementReportPresetDto Before, StockMovementReportPresetDto After)?> UpdateAsync(
        Guid id,
        SaveStockMovementReportPresetRequest request,
        Guid? actorId,
        CancellationToken ct = default)
    {
        var name = Validate(request);

        // Presets are shared, so an update without the version it started from is a lost update waiting to
        // happen. Creation has nothing to race with and does not carry one.
        if (request.Version is not { } version)
            throw new ValidationException("version", ErrorCode.Required,
                "The version the edit started from is required.");

        return await db.Database.ExecuteInTransactionAsync("statistics.preset.update", async () =>
        {
            await TakeWriteLockAsync(ct);

            var preset = await db.StockMovementReportPresets.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (preset is null) return ((StockMovementReportPresetDto, StockMovementReportPresetDto)?)null;

            var before = await ProjectAsync(id, ct);

            db.Entry(preset).Property<uint>("Version").OriginalValue = version;

            preset.Name = name;
            preset.Metrics = ToDomain(request.Metrics);
            preset.UpdatedAt = DateTime.UtcNow;
            preset.UpdatedById = actorId;

            try
            {
                await SaveAsync(ct);

                // Clearing the flag on the only default would leave the report with no preset to open
                // with, so the default moves rather than being switched off.
                if (request.IsDefault && !preset.IsDefault)
                    await SetDefaultAsync(preset, ct);
            }
            catch (DbUpdateConcurrencyException e)
            {
                throw new StockMovementPresetConflictException(id, e);
            }

            return (before, await ProjectAsync(id, ct));
        }, ct);
    }

    public async Task<StockMovementReportPresetDto?> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Database.ExecuteInTransactionAsync("statistics.preset.delete", async () =>
        {
            await TakeWriteLockAsync(ct);

            var preset = await db.StockMovementReportPresets.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (preset is null) return null;

            // Safe as a check-then-act only because the write lock is held: two people deleting the last
            // two presets at once would otherwise both pass and leave the report with no columns.
            if (await db.StockMovementReportPresets.CountAsync(ct) == 1)
                throw new ValidationException("root", ErrorCode.StockMovementPresetLastOne,
                    "The last preset cannot be deleted — the report has no columns without one.");

            var before = await ProjectAsync(id, ct);
            var wasDefault = preset.IsDefault;

            db.StockMovementReportPresets.Remove(preset);

            try
            {
                await db.SaveChangesAsync(ct);

                if (wasDefault)
                {
                    var successor =
                        await db.StockMovementReportPresets.OrderBy(p => p.Name).FirstOrDefaultAsync(ct);
                    if (successor is not null)
                        await SetDefaultAsync(successor, ct);
                }
            }
            catch (DbUpdateConcurrencyException e)
            {
                // The row carries an xmin token, so a concurrent edit makes the DELETE match nothing.
                throw new StockMovementPresetConflictException(id, e);
            }

            return before;
        }, ct);
    }

    // ---------- helpers ----------

    /// <summary>
    /// Takes the table-wide lock as the first statement of the caller's write transaction. Being
    /// transaction-scoped, it is released by the commit or the rollback and cannot outlive the request the
    /// way a session-scoped one on a pooled connection would.
    /// </summary>
    private Task TakeWriteLockAsync(CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", [WriteLockKey], ct);

    /// <summary>
    /// Two statements rather than one: the partial unique index on <c>IsDefault</c> is checked per
    /// statement, so the new default has to land after the old flag is already gone. Runs inside the
    /// caller's write transaction — never opens one of its own.
    /// </summary>
    private async Task SetDefaultAsync(StockMovementReportPreset preset, CancellationToken ct)
    {
        await db.StockMovementReportPresets
            .Where(p => p.IsDefault && p.Id != preset.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDefault, false), ct);

        preset.IsDefault = true;
        await db.SaveChangesAsync(ct);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (UniqueViolations.IsStockMovementPresetName(e))
        {
            throw new ValidationException("name", ErrorCode.StockMovementPresetNameDuplicate,
                "A preset with this name already exists.");
        }
    }

    /// <summary>Filtering and ordering happen on the entity, before this — a record is not translatable.</summary>
    private static IQueryable<PresetRow> Rows(IQueryable<StockMovementReportPreset> presets) =>
        presets
            .AsNoTracking()
            .Select(p => new PresetRow(
                p,
                EF.Property<uint>(p, "Version"),
                p.UpdatedById == null ? null : p.UpdatedBy!.FullName));

    private async Task<StockMovementReportPresetDto> ProjectAsync(Guid id, CancellationToken ct) =>
        ToDto(await Rows(db.StockMovementReportPresets.Where(p => p.Id == id)).FirstAsync(ct));

    private static StockMovementReportPresetDto ToDto(PresetRow row) =>
        new()
        {
            Id = row.Preset.Id,
            Name = row.Preset.Name,
            IsDefault = row.Preset.IsDefault,
            Metrics = row.Preset.Metrics
                .Select(m => new StockMovementMetricDto
                {
                    Name = m.Name,
                    Actions = m.Actions,
                    Directions = m.Directions,
                    ReceiptTagIds = m.ReceiptTagIds,
                })
                .ToList(),
            CreatedAt = row.Preset.CreatedAt,
            UpdatedAt = row.Preset.UpdatedAt,
            UpdatedById = row.Preset.UpdatedById,
            UpdatedByName = row.UpdatedByName,
            Version = row.Version,
        };

    private static List<StockMovementMetric> ToDomain(IReadOnlyList<StockMovementMetricDto> metrics) =>
        metrics.Select(m => new StockMovementMetric
        {
            Name = m.Name.Trim(),
            Actions = Empty(m.Actions),
            Directions = Empty(m.Directions),
            ReceiptTagIds = Empty(m.ReceiptTagIds),
        }).ToList();

    /// <summary>An empty array and «no predicate» mean the same thing; store one of them, not both.</summary>
    private static T[]? Empty<T>(T[]? values) => values is { Length: > 0 } ? values : null;

    private static string Validate(SaveStockMovementReportPresetRequest request)
    {
        for (var i = 0; i < request.Metrics.Count; i++)
        {
            var metric = request.Metrics[i];

            if (string.IsNullOrWhiteSpace(metric.Name))
                throw new ValidationException($"metrics[{i}].name", ErrorCode.Required,
                    "The metric name is required.");

            var unknown = metric.Actions?.FirstOrDefault(a => !StockMovementActions.All.Contains(a));
            if (unknown is not null)
                throw new ValidationException($"metrics[{i}].actions",
                    ErrorCode.StockMovementPresetUnknownAction, $"Unknown movement action '{unknown}'.");
        }

        return request.Name.Trim();
    }
}
