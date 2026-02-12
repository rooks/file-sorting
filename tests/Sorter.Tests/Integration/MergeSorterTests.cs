using System.Text;
using FileSorting.Shared.Progress;
using Xunit;

namespace FileSorting.Sorter.Tests.Integration;

public class MergeSorterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeTasksProgress _progress = new();

    public MergeSorterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sorter_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SortAsync_SmallFile_ProducesCorrectOutput()
    {
        var inputFile = Path.Combine(_tempDir, "input.txt");
        var outputFile = Path.Combine(_tempDir, "output.txt");

        await File.WriteAllTextAsync(inputFile, """
            5. Banana
            1. Apple
            3. Apple
            2. Cherry
            4. Banana
            """);

        var sorter = new MergeSorter(1024, 2, _progress);

        await sorter.SortAsync(inputFile, outputFile);

        var lines = await File.ReadAllLinesAsync(outputFile);
        Assert.Equal(5, lines.Length);
        Assert.Equal("1. Apple", lines[0]);
        Assert.Equal("3. Apple", lines[1]);
        Assert.Equal("4. Banana", lines[2]);
        Assert.Equal("5. Banana", lines[3]);
        Assert.Equal("2. Cherry", lines[4]);
    }

    [Fact]
    public async Task SortAsync_EmptyFile_ProducesEmptyOutput()
    {
        var inputFile = Path.Combine(_tempDir, "empty.txt");
        var outputFile = Path.Combine(_tempDir, "output.txt");

        await File.WriteAllTextAsync(inputFile, "");

        var sorter = new MergeSorter(1024, 2, _progress);

        await sorter.SortAsync(inputFile, outputFile);

        var content = await File.ReadAllTextAsync(outputFile);
        Assert.Empty(content);
    }

    [Fact]
    public async Task SortAsync_SingleLine_ProducesSameOutput()
    {
        var inputFile = Path.Combine(_tempDir, "single.txt");
        var outputFile = Path.Combine(_tempDir, "output.txt");

        await File.WriteAllTextAsync(inputFile, "42. Single Line\n");

        var sorter = new MergeSorter(1024, 2, _progress);

        await sorter.SortAsync(inputFile, outputFile);

        var lines = await File.ReadAllLinesAsync(outputFile);
        Assert.Single(lines);
        Assert.Equal("42. Single Line", lines[0]);
    }

    [Fact]
    public async Task SortAsync_MultipleChunks_MergesCorrectly()
    {
        var inputFile = Path.Combine(_tempDir, "multi.txt");
        var outputFile = Path.Combine(_tempDir, "output.txt");

        // Create input with many lines
        var sb = new StringBuilder();
        var random = new Random(42);
        var strings = new[] { "Alpha", "Beta", "Gamma", "Delta" };

        for (int i = 0; i < 1000; i++)
        {
            var num = random.Next(1, 1000000);
            var str = strings[random.Next(strings.Length)];
            sb.AppendLine($"{num}. {str}");
        }

        await File.WriteAllTextAsync(inputFile, sb.ToString());

        // Use small chunk size to force multiple chunks
        var sorter = new MergeSorter(1024, 2, _progress);

        await sorter.SortAsync(inputFile, outputFile);

        var lines = await File.ReadAllLinesAsync(outputFile);
        Assert.Equal(1000, lines.Length);

        // Verify sorting: strings should be in order
        string? lastString = null;
        long lastNumber = 0;

        foreach (var line in lines)
        {
            var parts = line.Split(". ", 2);
            var number = long.Parse(parts[0]);
            var str = parts[1];

            if (lastString != null)
            {
                var cmp = string.Compare(str, lastString, StringComparison.Ordinal);
                if (cmp == 0)
                {
                    Assert.True(number >= lastNumber, $"Numbers not sorted: {lastNumber} > {number} for '{str}'");
                }
                else
                {
                    Assert.True(cmp >= 0, $"Strings not sorted: '{lastString}' > '{str}'");
                }
            }

            lastString = str;
            lastNumber = number;
        }
    }

    [Fact]
    public async Task SortAsync_Cancellation_Throws()
    {
        var inputFile = Path.Combine(_tempDir, "cancel.txt");
        var outputFile = Path.Combine(_tempDir, "output.txt");

        // Create a moderately large file
        var sb = new StringBuilder();
        for (int i = 0; i < 10000; i++)
        {
            sb.AppendLine($"{i}. Line number {i}");
        }
        await File.WriteAllTextAsync(inputFile, sb.ToString());

        var sorter = new MergeSorter(1024, 2, _progress);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => sorter.SortAsync(inputFile, outputFile, cts.Token));
    }
}
