using BenchmarkDotNet.Attributes;

namespace FileSorting.Generator.Benchmarks;

[MemoryDiagnoser]
public class StringPoolBenchmarks
{
    private DictionaryStringPool _pool = null!;
    private int _poolCount;

    [GlobalSetup]
    public void Setup()
    {
        _pool = DictionaryStringPool.CreateDefault();
        _poolCount = _pool.Count;
    }

    [Benchmark]
    public byte[] GetString()
    {
        return _pool.GetString(0);
    }

    [Benchmark(OperationsPerInvoke = 1000)]
    public int GetStrings1000()
    {
        var totalLength = 0;
        for (var i = 0; i < 1000; i++)
        {
            totalLength += _pool.GetString(i % _poolCount).Length;
        }
        return totalLength;
    }
}
