using System.Diagnostics;
using DuckDB.NET.Data;
using FileSorting.Shared;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FileSorting.SorterDuckDb;

public sealed class DuckDbSortCommand : CancellableAsyncCommand<DuckDbSortSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        DuckDbSortSettings settings,
        CancellationToken t)
    {
        var inputPath = Path.GetFullPath(settings.Input!);
        var outputPath = Path.GetFullPath(settings.Output!);
        var inputInfo = new FileInfo(inputPath);

        AnsiConsole.MarkupLine($"[blue]Input:[/] {inputPath}");
        AnsiConsole.MarkupLine($"[blue]Output:[/] {outputPath}");
        AnsiConsole.MarkupLine($"[blue]Input size:[/] {SizeParser.Format(inputInfo.Length)}");
        AnsiConsole.MarkupLine($"[blue]Engine:[/] DuckDB");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Run(() => SortWithDuckDb(inputPath, outputPath), t);

            stopwatch.Stop();

            var outputInfo = new FileInfo(outputPath);
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

    private static void SortWithDuckDb(string inputPath, string outputPath)
    {
        var escapedInput = inputPath.Replace("'", "''");

        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();

        // Use all available cores
        cmd.CommandText = $"SET threads TO {Environment.ProcessorCount}";
        cmd.ExecuteNonQuery();

        // Load lines into a table, parsing number and string from the "number. string" format.
        // DuckDB read_csv delimiter is single-char only, so read whole lines and parse in SQL.
        cmd.CommandText =
            "CREATE TABLE lines AS " +
            "SELECT " +
            "  CAST(substr(line, 1, instr(line, '. ') - 1) AS BIGINT) AS number, " +
            "  substr(line, instr(line, '. ') + 2) AS string " +
            "FROM read_csv('" + escapedInput + "', " +
            "  columns={'line': 'VARCHAR'}, " +
            "  header=false, " +
            "  auto_detect=false, " +
            "  quote='', " +
            "  escape='')";
        cmd.ExecuteNonQuery();

        // Query sorted results and write output in the required format: "number. string\n"
        cmd.CommandText = "SELECT number, string FROM lines ORDER BY string, number";

        using var reader = cmd.ExecuteReader();
        using var writer = new StreamWriter(outputPath, false, new System.Text.UTF8Encoding(false),
            bufferSize: 16 * 1024 * 1024);

        while (reader.Read())
        {
            var number = reader.GetInt64(0);
            var str = reader.GetString(1);
            writer.Write(number);
            writer.Write(". ");
            writer.WriteLine(str);
        }
    }
}
