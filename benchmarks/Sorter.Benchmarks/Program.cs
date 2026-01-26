using BenchmarkDotNet.Running;
using FileSorting.Sorter.Benchmarks;


public static class Program
{
    public static void Main(string[] args)
    {
        // BenchmarkSwitcher.FromAssembly(typeof(SorterBenchmarks).Assembly).Run(args);

        // var summary = BenchmarkRunner.Run<SorterBenchmarks>();
        // var summary = BenchmarkRunner.Run<ParserBenchmarks>();
        var summary = BenchmarkRunner.Run<ChunkSorterBenchmarks>();

        Console.WriteLine(summary);
    }
}
