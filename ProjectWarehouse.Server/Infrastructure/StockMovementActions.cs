using System.Reflection;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Every value that can end up in <c>StockMovement.Action</c>. Collected from the two constant classes
/// that produce them, so a new action is validated the moment it is declared.
/// </summary>
public static class StockMovementActions
{
    public static IReadOnlySet<string> All { get; } = new[] { typeof(InventoryActions), typeof(TransferActions) }
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet();
}
