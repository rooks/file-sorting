using System.Diagnostics;
using FileSorting.Shared;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FileSorting.Sorter;

public sealed class SortCommand : AsyncCommand<SorterSettings>
{
    public static CancellationToken CancellationToken { get; set; }

    public override async Task<int> ExecuteAsync(CommandContext context, SorterSettings settings)
    {
        var inputInfo = new FileInfo(settings.Input!);

        var options = new SorterOptions
        {
            TempDirectory = settings.TempDir,
            ChunkSize = settings.ChunkSize != null ? (int)SizeParser.Parse(settings.ChunkSize) : CalculateDefaultChunkSize(),
            ParallelDegree = settings.Parallel ?? Environment.ProcessorCount
        };

        AnsiConsole.MarkupLine($"[blue]Input:[/] {inputInfo.FullName}");
        AnsiConsole.MarkupLine($"[blue]Output:[/] {Path.GetFullPath(settings.Output!)}");
        AnsiConsole.MarkupLine($"[blue]Input size:[/] {SizeParser.Format(inputInfo.Length)}");
        AnsiConsole.MarkupLine($"[blue]Chunk size:[/] {SizeParser.Format(options.ChunkSize)}");
        AnsiConsole.MarkupLine($"[blue]Parallelism:[/] {options.ParallelDegree}");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    ProgressTask? chunkingTask = null;
                    ProgressTask? mergingTask = null;

                    var progress = new Progress<SortProgress>(p =>
                    {
                        if (p.Phase == SortPhase.Chunking)
                        {
                            chunkingTask ??= ctx.AddTask("[green]Chunking[/]", maxValue: p.Total);
                            chunkingTask.Value = p.Current;
                        }
                        else if (p.Phase == SortPhase.Merging)
                        {
                            if (chunkingTask != null && !chunkingTask.IsFinished)
                            {
                                chunkingTask.Value = chunkingTask.MaxValue;
                            }
                            mergingTask ??= ctx.AddTask("[green]Merging[/]", maxValue: p.Total);
                            mergingTask.Value = p.Current;
                        }
                        else if (p.Phase == SortPhase.Completed)
                        {
                            if (mergingTask != null)
                            {
                                mergingTask.Value = mergingTask.MaxValue;
                            }
                        }
                    });

                    var sorter = new ExternalMergeSorter(options, progress);
                    await sorter.SortAsync(inputInfo.FullName, settings.Output!, CancellationToken);
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

    private static int CalculateDefaultChunkSize()
    {
        var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var memoryPerCore = (long)(availableMemory * Constants.MemoryUsageRatio / Environment.ProcessorCount);

        return (int)Math.Clamp(memoryPerCore, Constants.MinChunkSize, Constants.MaxChunkSize);
    }
}
