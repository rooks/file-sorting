using System.Text;
using FileSorting.Generator;
using Xunit;

namespace FileSorting.Generator.Tests;

public class LineGeneratorTests
{
    [Fact]
    public void WriteLine_ProducesValidFormat()
    {
        var pool = DictionaryStringPool.CreateDefault(seed: 42);
        var generator = new LineGenerator(pool, seed: 42);

        var buffer = new byte[1024];
        var bytesWritten = generator.WriteLine(buffer);

        Assert.True(bytesWritten > 0);

        var line = Encoding.UTF8.GetString(buffer, 0, bytesWritten);
        Assert.EndsWith("\n", line);
        Assert.Contains(". ", line);

        // Verify format: number. string\n
        var parts = line.TrimEnd('\n').Split(". ", 2);
        Assert.Equal(2, parts.Length);
        Assert.True(long.TryParse(parts[0], out var number));
        Assert.True(number > 0);
        Assert.True(parts[1].Length > 0);
    }

    [Fact]
    public void WriteLine_GeneratesNumbersInRange()
    {
        var pool = DictionaryStringPool.CreateDefault(seed: 42);
        var generator = new LineGenerator(pool, maxNumber: 100, seed: 42);

        var buffer = new byte[1024];
        for (var i = 0; i < 100; i++)
        {
            var bytesWritten = generator.WriteLine(buffer);
            var line = Encoding.UTF8.GetString(buffer, 0, bytesWritten);
            var parts = line.TrimEnd('\n').Split(". ", 2);
            var number = long.Parse(parts[0]);
            Assert.InRange(number, 1, 100);
        }
    }

    [Fact]
    public void WriteLine_DeterministicWithSeed()
    {
        var pool1 = DictionaryStringPool.CreateDefault(seed: 123);
        var gen1 = new LineGenerator(pool1, seed: 123);

        var pool2 = DictionaryStringPool.CreateDefault(seed: 123);
        var gen2 = new LineGenerator(pool2, seed: 123);

        var buffer1 = new byte[1024];
        var buffer2 = new byte[1024];

        for (var i = 0; i < 10; i++)
        {
            var bytes1 = gen1.WriteLine(buffer1);
            var bytes2 = gen2.WriteLine(buffer2);

            Assert.Equal(bytes1, bytes2);
            Assert.True(buffer1.AsSpan(0, bytes1).SequenceEqual(buffer2.AsSpan(0, bytes2)));
        }
    }
}
