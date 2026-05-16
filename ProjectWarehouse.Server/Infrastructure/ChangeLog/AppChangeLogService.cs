using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class AppChangeLogService(
    ApplicationDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IOptions<JsonOptions> jsonOptions)
    : AbstractChangeLogService(httpContextAccessor, jsonOptions)
{
    protected override IQueryable<ChangeLogEntry> GetGetChangelogQueryable() => db.ChangeLogEntries;

    protected override Task AddNewEntry(ChangeLogEntry entry)
    {
        db.ChangeLogEntries.Add(entry);
        return db.SaveChangesAsync();
    }
}