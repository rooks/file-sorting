using BenchmarkDotNet.Attributes;

namespace FileSorting.Generator.Benchmarks;

[MemoryDiagnoser]
public class LineGeneratorBenchmarks
{
    private LineGenerator _lineGenerator = null!;
    private byte[] _buffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        var pool = DictionaryStringPool.CreateDefault();
        _lineGenerator = new LineGenerator(pool, seed: 42);
        _buffer = new byte[1024];
    }

    [Benchmark]
    public int WriteLine()
    {
        return _lineGenerator.WriteLine(_buffer);
    }

    [Benchmark(OperationsPerInvoke = 1000)]
    public int WriteLines1000()
    {
        var totalBytes = 0;
        for (var i = 0; i < 1000; i++)
        {
            totalBytes += _lineGenerator.WriteLine(_buffer);
        }
        return totalBytes;
    }
}
