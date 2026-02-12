using Spectre.Console.Cli;

namespace FileSorting.Shared;

public abstract class CancellableAsyncCommand<T> : AsyncCommand<T>
    where T : CommandSettings
{
    private readonly CancellationTokenSource _cts = new();

    protected CancellableAsyncCommand()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, e) =>
        {
            if (_cts.IsCancellationRequested) return;
            _cts.Cancel();
        };
    }

    protected abstract Task<int> ExecuteAsync(CommandContext context, T settings, CancellationToken t);

    public sealed override Task<int> ExecuteAsync(CommandContext context, T settings) =>
        ExecuteAsync(context, settings, _cts.Token);
}
