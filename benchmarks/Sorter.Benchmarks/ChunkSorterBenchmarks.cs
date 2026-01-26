using System.Text;
using BenchmarkDotNet.Attributes;

namespace FileSorting.Sorter.Benchmarks;

[MemoryDiagnoser]
public class ChunkSorterBenchmarks
{
    private byte[] _chunkData = null!;

    [Params(1000, 10000)]
    public int LineCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        var strings = new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };
        var sb = new StringBuilder();

        for (var i = 0; i < LineCount; i++)
        {
            var num = random.Next(1, 1000000);
            var str = strings[random.Next(strings.Length)];
            sb.AppendLine($"{num}. {str}");
        }

        _chunkData = Encoding.UTF8.GetBytes(sb.ToString());
    }

    [Benchmark]
    public int SortChunk()
    {
        var sorted = ChunkSorter.SortChunk(_chunkData.AsMemory());
        return sorted.Count;
    }
}
