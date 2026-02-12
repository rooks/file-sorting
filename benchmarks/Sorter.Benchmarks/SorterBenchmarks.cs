using BenchmarkDotNet.Attributes;
using FileSorting.Generator;
using FileSorting.Shared;
using FileSorting.Shared.Progress;

namespace FileSorting.Sorter.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class SorterBenchmarks
{
    private readonly FakeTasksProgress _progress = new();

    private string _inputFile = null!;
    private string _outputFile = null!;
    private string _tempDir = null!;

    [Params(10 * 1024 * 1024, 100 * 1024 * 1024)] // 10MB, 100MB
    public int FileSize { get; set; }

    [Params(8 * 1024 * 1024, 32 * 1024 * 1024)] // 8MB, 32MB chunks
    public int ChunkSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _inputFile = Path.Combine(_tempDir, "input.txt");
        _outputFile = Path.Combine(_tempDir, "output.txt");

        var pool = DictionaryStringPool.CreateDefault();
        var fileGen = new FileGenerator(pool, _progress);
        fileGen.GenerateAsync(_inputFile, FileSize, seed: 42).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Benchmark]
    public async Task ExternalMergeSort()
    {
        var sorter = new MergeSorter(
            ChunkSize,
            Environment.ProcessorCount,
            _progress,
            _tempDir);
        await sorter.SortAsync(_inputFile, _outputFile);
    }
}
