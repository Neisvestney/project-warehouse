using System.Collections.Concurrent;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ProjectWarehouse.Ops.Ui;

/// Transferred bytes in the tool's own units. Steps that move nothing render blank rather than a
/// misleading zero, so the column only speaks for the ones it was told about.
public sealed class TransferColumn : ProgressColumn
{
    private readonly ConcurrentDictionary<int, long?> _totals = new();

    protected override bool NoWrap => true;

    public void Track(ProgressTask task, long? total) => _totals[task.Id] = total;

    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        if (!_totals.TryGetValue(task.Id, out var total))
            return new Text(string.Empty);

        var done = ByteSize.Format((long)task.Value);

        return new Text(
            total is { } max ? $"{done}/{ByteSize.Format(max)}" : done,
            new Style(foreground: Color.Grey));
    }
}
