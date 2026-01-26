using FileSorting.Shared;
using Xunit;

namespace FileSorting.Shared.Tests;

public class SizeParserTests
{
    [Theory]
    [InlineData("1B", 1L)]
    [InlineData("1KB", 1024L)]
    [InlineData("1K", 1024L)]
    [InlineData("1MB", 1024L * 1024)]
    [InlineData("1M", 1024L * 1024)]
    [InlineData("1GB", 1024L * 1024 * 1024)]
    [InlineData("1G", 1024L * 1024 * 1024)]
    [InlineData("1TB", 1024L * 1024 * 1024 * 1024)]
    [InlineData("100", 100L)]
    public void Parse_ValidInput_ReturnsCorrectBytes(string input, long expected)
    {
        var result = SizeParser.Parse(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.5KB", 1536L)]
    [InlineData("2.5MB", (long)(2.5 * 1024 * 1024))]
    public void Parse_DecimalInput_ReturnsCorrectBytes(string input, long expected)
    {
        var result = SizeParser.Parse(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyOrNull_ThrowsArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => SizeParser.Parse(input!));
    }

    [Theory]
    [InlineData("KB")]
    [InlineData("invalid")]
    public void Parse_InvalidFormat_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => SizeParser.Parse(input));
    }

    [Fact]
    public void Parse_UnknownSuffix_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => SizeParser.Parse("1PB"));
    }

    [Theory]
    [InlineData("1KB", true)]
    [InlineData("invalid", false)]
    public void TryParse_ReturnsExpectedResult(string input, bool expectedResult)
    {
        var result = SizeParser.TryParse(input, out var bytes);
        Assert.Equal(expectedResult, result);
        if (expectedResult)
        {
            Assert.True(bytes > 0);
        }
    }

    [Theory]
    [InlineData(1L, "1B")]
    [InlineData(1024L, "1KB")]
    [InlineData(1024L * 1024, "1MB")]
    [InlineData(1024L * 1024 * 1024, "1GB")]
    public void Format_ReturnsExpectedString(long bytes, string expected)
    {
        var result = SizeParser.Format(bytes);
        Assert.Equal(expected, result);
    }
}
