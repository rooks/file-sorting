using System.Diagnostics;
using FileSorting.Shared;
using FileSorting.Shared.Progress;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FileSorting.Sorter;

public sealed class SortCommand : CancellableAsyncCommand<SorterSettings>
{
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        SorterSettings settings,
        CancellationToken ct)
    {
        var inputInfo = new FileInfo(settings.Input!);
        var chunkSize = GetChunkSize(settings.ChunkSize);
        var parallelDegree = settings.Parallel is null or 0 ? Environment.ProcessorCount : settings.Parallel.Value;

        AnsiConsole.MarkupLine($"[blue]Input:[/] {inputInfo.FullName}");
        AnsiConsole.MarkupLine($"[blue]Output:[/] {Path.GetFullPath(settings.Output!)}");
        AnsiConsole.MarkupLine($"[blue]Input size:[/] {SizeParser.Format(inputInfo.Length)}");
        AnsiConsole.MarkupLine($"[blue]Chunk size:[/] {SizeParser.Format(chunkSize)}");
        AnsiConsole.MarkupLine($"[blue]Parallelism:[/] {parallelDegree}");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await AnsiConsole.Progress()
                .AutoClear(true)
                .HideCompleted(true)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn()
                )
                .StartAsync(async ctx =>
                {
                    using var progress = new SpectreTasksProgress(ctx);

                    var sorter = new ExternalMergeSorter(chunkSize, parallelDegree, progress, settings.TempDir);
                    await sorter.SortAsync(inputInfo.FullName, settings.Output!, ct);
                });

            stopwatch.Stop();

            var outputInfo = new FileInfo(settings.Output!);
            AnsiConsole.MarkupLine("[green]Sorting complete.[/]");
            AnsiConsole.MarkupLine($"[blue]Time:[/] {stopwatch.Elapsed.TotalSeconds:F2}s");
            AnsiConsole.MarkupLine($"[blue]Speed:[/] {SizeParser.Format((long)(inputInfo.Length / stopwatch.Elapsed.TotalSeconds))}/s");
            AnsiConsole.MarkupLine($"[blue]Output size:[/] {SizeParser.Format(outputInfo.Length)}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Sorting cancelled.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private static int GetChunkSize(string? size)
    {
        if (!string.IsNullOrEmpty(size))
            return (int)SizeParser.Parse(size);

        var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var memoryPerCore = (long)(availableMemory * Constants.MemoryUsageRatio / Environment.ProcessorCount);

        return (int)Math.Clamp(memoryPerCore, Constants.MinChunkSize, Constants.MaxChunkSize);
    }
}
