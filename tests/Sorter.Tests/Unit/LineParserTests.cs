using System.Text;
using FileSorting.Sorter;
using Xunit;

namespace FileSorting.Sorter.Tests.Unit;

public class LineParserTests
{
    [Fact]
    public void Parse_ValidLine_ReturnsCorrectParsedLine()
    {
        var line = "12345. Hello World"u8.ToArray();
        var memory = line.AsMemory();

        var result = LineParser.Parse(memory);

        Assert.Equal("12345", Encoding.UTF8.GetString(result.NumberPart));
        Assert.Equal("Hello World", Encoding.UTF8.GetString(result.StringPart));
        Assert.Equal(12345L, result.GetNumber());
        Assert.Equal("Hello World", result.GetString());
    }

    [Fact]
    public void Parse_LargeNumber_ReturnsCorrectParsedLine()
    {
        var line = "999999999999. Test"u8.ToArray();
        var memory = line.AsMemory();

        var result = LineParser.Parse(memory);

        Assert.Equal(999999999999L, result.GetNumber());
        Assert.Equal("Test", result.GetString());
    }

    [Fact]
    public void Parse_StringWithSpaces_ReturnsCorrectParsedLine()
    {
        var line = "1. Hello World With Spaces"u8.ToArray();
        var memory = line.AsMemory();

        var result = LineParser.Parse(memory);

        Assert.Equal("Hello World With Spaces", result.GetString());
    }

    [Fact]
    public void Parse_InvalidFormat_ThrowsFormatException()
    {
        var line = "no separator here"u8.ToArray();
        var memory = line.AsMemory();

        Assert.Throws<FormatException>(() => LineParser.Parse(memory));
    }

    [Theory]
    [InlineData("12345. Hello", true)]
    [InlineData("no separator", false)]
    public void TryParse_ReturnsExpectedResult(string line, bool expectedResult)
    {
        var bytes = Encoding.UTF8.GetBytes(line);
        var memory = bytes.AsMemory();

        var result = LineParser.TryParse(memory, out var parsed);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void FindNewline_ReturnsCorrectIndex()
    {
        var buffer = "Hello\nWorld"u8;
        var index = LineParser.FindNewline(buffer);
        Assert.Equal(5, index);
    }

    [Fact]
    public void FindNewline_NoNewline_ReturnsMinusOne()
    {
        var buffer = "Hello World"u8;
        var index = LineParser.FindNewline(buffer);
        Assert.Equal(-1, index);
    }

    [Fact]
    public void FindLastNewline_ReturnsCorrectIndex()
    {
        var buffer = "Hello\nWorld\n"u8;
        var index = LineParser.FindLastNewline(buffer);
        Assert.Equal(11, index);
    }
}
