using BenchmarkDotNet.Attributes;
using FileSorting.Shared;

namespace FileSorting.Sorter.Benchmarks;

[MemoryDiagnoser]
public class ParserBenchmarks
{
    private byte[] _lineData = null!;

    [GlobalSetup]
    public void Setup()
    {
        _lineData = "12345678. This is a sample line with some content for benchmarking"u8.ToArray();
    }

    [Benchmark]
    public ParsedLine ParseLine()
    {
        return LineParser.Parse(_lineData.AsMemory());
    }

    [Benchmark]
    public int CompareTwoLines()
    {
        var line1 = LineParser.Parse("100. Apple"u8.ToArray().AsMemory());
        var line2 = LineParser.Parse("50. Apple"u8.ToArray().AsMemory());
        return ParsedLineComparer.Compare(in line1, in line2);
    }
}