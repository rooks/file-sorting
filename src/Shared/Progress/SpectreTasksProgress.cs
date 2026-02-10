using Spectre.Console;

namespace FileSorting.Shared.Progress;

public class SpectreTasksProgress(ProgressContext ctx) : ITasksProgress, IDisposable
{
    private ProgressTask _task = new(0, "dummy", 1, false);
    private string _title = string.Empty;

    public void Start(string title, double max)
    {
        if (_title != title)
            _task.StopTask();

        _title = title;
        _task = ctx.AddTask($"[green]{title}[/]", maxValue: max);
    }

    public void Stop()
    {
        _title = string.Empty;
        _task.StopTask();
    }

    public void Update(double value)
    {
        _task.Value = value;
    }

    public void Dispose()
    {
        Stop();
    }
}
