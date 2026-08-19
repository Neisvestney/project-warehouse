using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Realtime;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class AppChangeLogService(
    ApplicationDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IRealtimeNotifier realtime,
    IOptions<JsonOptions> jsonOptions)
    : AbstractChangeLogService(httpContextAccessor, realtime, jsonOptions)
{
    protected override IQueryable<ChangeLogEntry> GetGetChangelogQueryable() => db.ChangeLogEntries;

    protected override Task AddNewEntry(ChangeLogEntry entry)
    {
        db.ChangeLogEntries.Add(entry);
        return db.SaveChangesAsync();
    }
}