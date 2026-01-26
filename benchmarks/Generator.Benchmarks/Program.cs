using BenchmarkDotNet.Running;

namespace FileSorting.Generator.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        // BenchmarkSwitcher.FromAssembly(typeof(StringPoolBenchmarks).Assembly).Run(args);

        var summary = BenchmarkRunner.Run<StringPoolBenchmarks>();
        // var summary = BenchmarkRunner.Run<LineGeneratorBenchmarks>();
        // var summary = BenchmarkRunner.Run<FileGeneratorBenchmarks>();

        Console.WriteLine(summary);
    }
}
