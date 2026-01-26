using BenchmarkDotNet.Attributes;

namespace FileSorting.Generator.Benchmarks;

[MemoryDiagnoser]
public class StringPoolBenchmarks
{
    private StringPool _pool = null!;

    [Params(0.0, 0.3, 0.7)]
    public double DuplicateRatio { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _pool = new StringPool(duplicateRatio: DuplicateRatio, seed: 42);
    }

    [Benchmark]
    public byte[] GetString()
    {
        return _pool.GetString();
    }

    [Benchmark(OperationsPerInvoke = 1000)]
    public int GetStrings1000()
    {
        var totalLength = 0;
        for (var i = 0; i < 1000; i++)
        {
            totalLength += _pool.GetString().Length;
        }
        return totalLength;
    }
}
