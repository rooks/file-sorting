using System.CommandLine;
using FileSorting.Generator;
using FileSorting.Shared;

var sizeOption = new Option<string>("--size") { Description = "Target file size (e.g., 1GB, 500MB, 100KB)" };
var outputOption = new Option<FileInfo>("--output") { Description = "Output file path" };
var duplicateRatioOption = new Option<double>("--duplicate-ratio")
{
    Description = "Ratio of duplicate strings (0.0 to 1.0)",
    DefaultValueFactory = _ => 0.3
};
var seedOption = new Option<int?>("--seed") { Description = "Random seed for reproducible output" };

var rootCommand = new RootCommand("Generates test files for the sorting algorithm")
{
    Options =
    {
        sizeOption,
        outputOption,
        duplicateRatioOption,
        seedOption,
    }
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var size = parseResult.GetValue(sizeOption);
    var output = parseResult.GetValue(outputOption);
    var duplicateRatio = parseResult.GetValue(duplicateRatioOption);
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
    Console.WriteLine($"Duplicate ratio: {duplicateRatio:P0}");

    var stringPool = new StringPool(duplicateRatio, seed: seed);
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
