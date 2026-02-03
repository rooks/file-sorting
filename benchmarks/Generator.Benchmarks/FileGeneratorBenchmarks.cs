using BenchmarkDotNet.Attributes;

namespace FileSorting.Generator.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class FileGeneratorBenchmarks
{
    private DictionaryStringPool _pool = null!;
    private ParallelFileGenerator _fileGenerator = null!;
    private string _tempDir = null!;
    private string _outputFile = null!;

    [Params(1024 * 1024, 10 * 1024 * 1024)] // 1MB, 10MB
    public int FileSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"genbench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _outputFile = Path.Combine(_tempDir, "output.txt");

        _pool = DictionaryStringPool.CreateDefault();
        _fileGenerator = new ParallelFileGenerator(_pool);
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
    public Task GenerateFile()
    {
        return _fileGenerator.GenerateAsync(_outputFile, FileSize, seed: 42);
    }
}
