using BenchmarkDotNet.Attributes;

namespace FileSorting.Generator.Benchmarks;

[MemoryDiagnoser]
public class StringPoolBenchmarks
{
    private DictionaryStringPool _pool = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pool = DictionaryStringPool.CreateDefault(seed: 42);
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
