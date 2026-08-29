using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Recognises the unique violations the application reports as business outcomes. A violation counts only
/// when it names the expected index — anything else landing in the same save is a genuine failure and must
/// not be dressed up as a duplicate.
/// </summary>
public static class UniqueViolations
{
    private const string UnitInventoryNumberIndexName = "IX_InventoryItems_CatalogItemId_InventoryNumber";

    /// <summary>The partial unique index behind "this inventory number is already taken for that catalog item".</summary>
    public static bool IsUnitInventoryNumber(Exception e) =>
        e is DbUpdateException { InnerException: PostgresException pg }
        && pg.SqlState == PostgresErrorCodes.UniqueViolation
        && pg.ConstraintName == UnitInventoryNumberIndexName;
}
