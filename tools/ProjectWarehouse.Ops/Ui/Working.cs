using Spectre.Console;

namespace ProjectWarehouse.Ops.Ui;

public static class Working
{
    /// One row that spins while the work runs and keeps a tick once it is done. Rendered as a
    /// progress row rather than a status: a status erases itself and restores the cursor when it
    /// ends, and whatever is printed next lands on top of the line above.
    public static async Task<T> RunAsync<T>(string title, Func<Task<T>> work)
    {
        var result = default(T)!;

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new SpinnerColumn
                {
                    CompletedText = "✓",
                    CompletedStyle = new Style(foreground: Color.Green),
                },
                new TaskDescriptionColumn { Alignment = Justify.Left })
            .StartAsync(async context =>
            {
                var task = context.AddTask(Markup.Escape(title), maxValue: 1d);
                task.IsIndeterminate = true;

                result = await work();

                // Left unfinished when the work throws, so the tick only ever marks work that got
                // there.
                task.IsIndeterminate = false;
                task.Value = task.MaxValue;
                task.StopTask();
            });

        return result;
    }
}
