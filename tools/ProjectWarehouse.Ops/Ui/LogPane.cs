using Spectre.Console;
using Spectre.Console.Rendering;

namespace ProjectWarehouse.Ops.Ui;

/// A fixed-height tail of a command's output. Lines arrive on the process reader threads and are
/// rendered by the live display, so the buffer is the synchronization point between the two.
public sealed class LogPane(int capacity)
{
    private readonly Queue<string> _lines = new();
    private readonly Lock _gate = new();
    private string _header = string.Empty;

    public void Write(string header, string line)
    {
        lock (_gate)
        {
            if (header != _header)
            {
                _lines.Clear();
                _header = header;
            }

            _lines.Enqueue(line.TrimEnd());

            while (_lines.Count > capacity)
                _lines.Dequeue();
        }
    }

    public IRenderable Render()
    {
        string header;
        string[] lines;

        lock (_gate)
        {
            header = _header;
            lines = [.. _lines];
        }

        // A wrapped line would push the pane past its own height and make the tail scroll away.
        var width = Math.Max(20, AnsiConsole.Profile.Width - 4);
        var body = lines.Length == 0
            ? "[grey]waiting for output…[/]"
            : string.Join(
                '\n',
                lines.Select(line => Markup.Escape(
                    line.Length <= width ? line : line[..width])));

        return new Panel(new Markup(body))
        {
            Header = new PanelHeader(header.Length == 0 ? " " : $" {Markup.Escape(header)} "),
            Border = BoxBorder.Rounded,
            Expand = true,
        };
    }
}
