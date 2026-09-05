using ProjectWarehouse.Ops.Services;
using Spectre.Console;

namespace ProjectWarehouse.Ops.Ui;

/// One row per step. A transfer whose size cannot be asked for up front — pg_dump only reveals it
/// by finishing — runs indeterminate and settles on its real size when the step ends.
public sealed class ProgressReporter(ProgressContext context, TransferColumn column) : IStepReporter
{
    public static Task<T> RunAsync<T>(Func<IStepReporter, Task<T>> scenario)
    {
        var column = new TransferColumn();

        return AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                column,
                new SpinnerColumn
                {
                    CompletedText = "✓",
                    CompletedStyle = new Style(foreground: Color.Green),
                })
            .StartAsync(context => scenario(new ProgressReporter(context, column)));
    }

    public IStep Begin(string description, long? totalBytes = null)
    {
        var task = context.AddTask(Markup.Escape(description), maxValue: totalBytes ?? 1d);
        task.IsIndeterminate = totalBytes is null;

        return new Step(task, column, totalBytes);
    }

    private sealed class Step : IStep
    {
        private readonly ProgressTask _task;
        private readonly TransferColumn _column;
        private readonly long? _total;
        private long _transferred;
        private bool _tracked;
        private bool _completed;

        public Step(ProgressTask task, TransferColumn column, long? total)
        {
            _task = task;
            _column = column;
            _total = total;

            if (total is null)
                return;

            column.Track(task, total);
            _tracked = true;
        }

        /// The measured total counts file contents; the tar adds a header per file on top, so the
        /// last few kilobytes arrive with the bar already full.
        public void Report(long value)
        {
            _transferred = _total is { } max ? Math.Min(value, max) : value;
            _task.Value = _transferred;

            if (_tracked)
                return;

            _column.Track(_task, null);
            _tracked = true;
        }

        public void Complete()
        {
            // An unknown total is whatever arrived, so the row ends full and reads as its own size.
            if (_total is null)
                _task.MaxValue = Math.Max(_transferred, 1);

            _task.IsIndeterminate = false;
            _task.Value = _task.MaxValue;
            _task.StopTask();
            _completed = true;
        }

        /// Left unfinished on purpose when the step did not complete: the tick belongs to work that
        /// got there, and the row keeps the bar where it stopped.
        public void Dispose()
        {
            if (_completed)
                return;

            _task.IsIndeterminate = false;
        }
    }
}
