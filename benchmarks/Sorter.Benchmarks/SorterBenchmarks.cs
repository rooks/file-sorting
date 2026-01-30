using BenchmarkDotNet.Attributes;
using FileSorting.Generator;

namespace FileSorting.Sorter.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class SorterBenchmarks
{
    private string _inputFile = null!;
    private string _outputFile = null!;
    private string _tempDir = null!;

    [Params(1024 * 1024, 10 * 1024 * 1024)] // 1MB, 10MB
    public int FileSize { get; set; }

    [Params(1024 * 1024, 2 * 1024 * 1024)] // 1MB, 2MB chunks
    public int ChunkSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _inputFile = Path.Combine(_tempDir, "input.txt");
        _outputFile = Path.Combine(_tempDir, "output.txt");

        // Generate test file
        var pool = DictionaryStringPool.CreateDefault();
        var fileGen = new ParallelFileGenerator(pool, Environment.ProcessorCount);
        fileGen.GenerateAsync(_inputFile, FileSize, seed: 42).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Benchmark]
    public async Task ExternalMergeSort()
    {
        var options = new SorterOptions
        {
            ChunkSize = ChunkSize,
            ParallelDegree = Environment.ProcessorCount,
            TempDirectory = _tempDir
        };

        var sorter = new ExternalMergeSorter(options);
        await sorter.SortAsync(_inputFile, _outputFile);
    }
}
