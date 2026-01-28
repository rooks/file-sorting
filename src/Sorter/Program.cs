using System.CommandLine;
using FileSorting.Shared;
using FileSorting.Sorter;

var inputOption = new Option<FileInfo>("--input") { Description = "Input file to sort" };
var outputOption = new Option<FileInfo>("--output") { Description = "Output file path" };
var tempDirOption = new Option<DirectoryInfo?>("--temp-dir") { Description = "Temporary directory for chunks" };
var chunkSizeOption = new Option<string?>("--chunk-size") { Description = "Chunk size (e.g., 64MB, 128MB)" };
var parallelOption = new Option<int?>("--parallel") { Description = "Degree of parallelism" };

var rootCommand = new RootCommand("Sorts large files using external merge sort");
rootCommand.Options.Add(inputOption);
rootCommand.Options.Add(outputOption);
rootCommand.Options.Add(tempDirOption);
rootCommand.Options.Add(chunkSizeOption);
rootCommand.Options.Add(parallelOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var input = parseResult.GetValue(inputOption);
    var output = parseResult.GetValue(outputOption);
    var tempDir = parseResult.GetValue(tempDirOption);
    var chunkSizeStr = parseResult.GetValue(chunkSizeOption);
    var parallel = parseResult.GetValue(parallelOption);

    if (input == null || !input.Exists)
    {
        Console.Error.WriteLine("Error: --input is required and must exist");
        return 1;
    }

    if (output == null)
    {
        Console.Error.WriteLine("Error: --output is required");
        return 1;
    }

    var options = new SorterOptions
    {
        TempDirectory = tempDir?.FullName,
        ChunkSize = chunkSizeStr != null ? (int)SizeParser.Parse(chunkSizeStr) : new SorterOptions().ChunkSize,
        ParallelDegree = parallel ?? Environment.ProcessorCount
    };

    Console.WriteLine($"Sorting: {input.FullName}");
    Console.WriteLine($"Output: {output.FullName}");
    Console.WriteLine($"Input size: {SizeParser.Format(input.Length)}");
    Console.WriteLine($"Chunk size: {SizeParser.Format(options.ChunkSize)}");
    Console.WriteLine($"Parallelism: {options.ParallelDegree}");

    var progress = new Progress<SortProgress>(p =>
    {
        var phase = p.Phase switch
        {
            SortPhase.Chunking => "Chunking",
            SortPhase.Merging => "Merging",
            SortPhase.Completed => "Completed",
            _ => "Unknown"
        };

        if (p.Phase == SortPhase.Chunking)
        {
            Console.Write($"\r{phase}: {SizeParser.Format(p.Current)} / {SizeParser.Format(p.Total)} ({p.Percentage:F1}%)");
        }
        else if (p.Phase == SortPhase.Merging)
        {
            Console.Write($"\r{phase}: {p.Current} / {p.Total} chunks");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine($"{phase}!");
        }
    });

    var sorter = new ExternalMergeSorter(options, progress);
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        await sorter.SortAsync(input.FullName, output.FullName, cancellationToken);
        stopwatch.Stop();

        var outputInfo = new FileInfo(output.FullName);
        Console.WriteLine($"Time: {stopwatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"Speed: {SizeParser.Format((long)(input.Length / stopwatch.Elapsed.TotalSeconds))}/s");
        Console.WriteLine($"Output size: {SizeParser.Format(outputInfo.Length)}");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine();
        Console.WriteLine("Sorting cancelled.");
        return 1;
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
});

return await rootCommand.Parse(args).InvokeAsync();
