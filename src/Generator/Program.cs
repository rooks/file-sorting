using System.CommandLine;
using FileSorting.Generator;
using FileSorting.Shared;

var sizeOption = new Option<string>("--size") { Description = "Target file size (e.g., 1GB, 500MB, 100KB)" };
var outputOption = new Option<FileInfo>("--output") { Description = "Output file path" };
var dictionaryOption = new Option<FileInfo?>("--dictionary")
{
    Description = "Path to dictionary file (one word/phrase per line). If not specified, uses embedded default dictionary."
};
var seedOption = new Option<int?>("--seed") { Description = "Random seed for reproducible output" };

var rootCommand = new RootCommand("Generates test files for the sorting algorithm")
{
    Options =
    {
        sizeOption,
        outputOption,
        dictionaryOption,
        seedOption,
    }
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var size = parseResult.GetValue(sizeOption);
    var output = parseResult.GetValue(outputOption);
    var dictionary = parseResult.GetValue(dictionaryOption);
    var seed = parseResult.GetValue(seedOption);

    if (string.IsNullOrEmpty(size))
    {
        Console.Error.WriteLine("Error: --size is required");
        return 1;
    }

    if (output == null)
    {
        Console.Error.WriteLine("Error: --output is required");
        return 1;
    }

    var targetBytes = SizeParser.Parse(size);
    Console.WriteLine($"Generating {SizeParser.Format(targetBytes)} file: {output.FullName}");

    DictionaryStringPool stringPool;
    if (dictionary != null)
    {
        if (!dictionary.Exists)
        {
            Console.Error.WriteLine($"Error: Dictionary file not found: {dictionary.FullName}");
            return 1;
        }
        Console.WriteLine($"Using dictionary: {dictionary.FullName}");
        stringPool = DictionaryStringPool.FromFile(dictionary.FullName, seed);
    }
    else
    {
        Console.WriteLine("Using default dictionary");
        stringPool = DictionaryStringPool.CreateDefault(seed);
    }

    var lineGenerator = new LineGenerator(stringPool, seed: seed);

    var progress = new Progress<long>(bytes =>
    {
        var percent = (double)bytes / targetBytes * 100;
        Console.Write($"\rProgress: {SizeParser.Format(bytes)} / {SizeParser.Format(targetBytes)} ({percent:F1}%)");
    });

    var fileGenerator = new FileGenerator(lineGenerator, progress);

    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        await fileGenerator.GenerateAsync(output.FullName, targetBytes, cancellationToken);
        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine("Generation complete.");
        Console.WriteLine($"Time: {stopwatch.Elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"Speed: {SizeParser.Format((long)(targetBytes / stopwatch.Elapsed.TotalSeconds))}/s");
        return 0;
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine();
        Console.WriteLine("Generation cancelled.");
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
