using System.Text;
using FileSorting.Shared;
using Xunit;

namespace FileSorting.Shared.Tests;

public class ParsedLineComparerTests
{
    private static ParsedLine CreateParsedLine(string line)
    {
        var bytes = Encoding.UTF8.GetBytes(line);
        return LineParser.Parse(bytes.AsMemory());
    }

    [Fact]
    public void Compare_DifferentStrings_SortsByString()
    {
        var a = CreateParsedLine("1. Apple");
        var b = CreateParsedLine("2. Banana");

        var result = ParsedLineComparer.Compare(in a, in b);

        Assert.True(result < 0); // Apple < Banana
    }

    [Fact]
    public void Compare_SameString_SortsByNumber()
    {
        var a = CreateParsedLine("100. Same");
        var b = CreateParsedLine("50. Same");

        var result = ParsedLineComparer.Compare(in a, in b);

        Assert.True(result > 0); // 100 > 50
    }

    [Fact]
    public void Compare_IdenticalLines_ReturnsZero()
    {
        var a = CreateParsedLine("42. Same");
        var b = CreateParsedLine("42. Same");

        var result = ParsedLineComparer.Compare(in a, in b);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Compare_SortsCorrectly()
    {
        var lines = new[]
        {
            CreateParsedLine("5. Banana"),
            CreateParsedLine("1. Apple"),
            CreateParsedLine("3. Apple"),
            CreateParsedLine("2. Cherry"),
        };

        var sorted = lines.ToList();
        sorted.Sort(ParsedLineComparerWrapper.Instance);

        Assert.Equal("1. Apple", sorted[0].ToString());
        Assert.Equal("3. Apple", sorted[1].ToString());
        Assert.Equal("5. Banana", sorted[2].ToString());
        Assert.Equal("2. Cherry", sorted[3].ToString());
    }
}
