using System.Diagnostics;
using FileSorting.Shared;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FileSorting.Generator;

public sealed class GenerateCommand : AsyncCommand<GeneratorSettings>
{
    public static CancellationToken CancellationToken { get; set; }

    public override async Task<int> ExecuteAsync(CommandContext context, GeneratorSettings settings)
    {
        var targetBytes = SizeParser.Parse(settings.Size!);

        AnsiConsole.MarkupLine($"[blue]Output:[/] {Path.GetFullPath(settings.Output!)}");
        AnsiConsole.MarkupLine($"[blue]Target size:[/] {SizeParser.Format(targetBytes)}");

        DictionaryStringPool stringPool;
        if (settings.Dictionary != null)
        {
            AnsiConsole.MarkupLine($"[blue]Dictionary:[/] {settings.Dictionary}");
            stringPool = DictionaryStringPool.FromFile(settings.Dictionary);
        }
        else
        {
            AnsiConsole.MarkupLine("[blue]Dictionary:[/] default");
            stringPool = DictionaryStringPool.CreateDefault();
        }

        var workerCount = Environment.ProcessorCount;
        AnsiConsole.MarkupLine($"[blue]Workers:[/] {workerCount}");

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
                    var task = ctx.AddTask("[green]Generating[/]", maxValue: targetBytes);

                    var progress = new Progress<long>(bytes =>
                    {
                        task.Value = bytes;
                    });

                    var fileGenerator = new ParallelFileGenerator(stringPool, workerCount, progress);
                    await fileGenerator.GenerateAsync(settings.Output!, targetBytes, settings.Seed, CancellationToken);

                    task.Value = targetBytes;
                });

            stopwatch.Stop();

            AnsiConsole.MarkupLine("[green]Generation complete.[/]");
            AnsiConsole.MarkupLine($"[blue]Time:[/] {stopwatch.Elapsed.TotalSeconds:F2}s");
            AnsiConsole.MarkupLine($"[blue]Speed:[/] {SizeParser.Format((long)(targetBytes / stopwatch.Elapsed.TotalSeconds))}/s");
            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Generation cancelled.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
