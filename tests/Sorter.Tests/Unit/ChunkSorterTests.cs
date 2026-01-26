using System.Text;
using FileSorting.Sorter;
using Xunit;

namespace FileSorting.Sorter.Tests.Unit;

public class ChunkSorterTests
{
    [Fact]
    public void SortChunk_SortsLinesByStringThenNumber()
    {
        var chunk = """
            5. Banana
            1. Apple
            3. Apple
            2. Cherry

            """u8.ToArray();

        var sorted = ChunkSorter.SortChunk(chunk.AsMemory());

        Assert.Equal(4, sorted.Count);
        Assert.Equal("1. Apple", sorted[0].ToString());
        Assert.Equal("3. Apple", sorted[1].ToString());
        Assert.Equal("5. Banana", sorted[2].ToString());
        Assert.Equal("2. Cherry", sorted[3].ToString());
    }

    [Fact]
    public void SortChunk_HandlesEmptyChunk()
    {
        var chunk = Array.Empty<byte>();

        var sorted = ChunkSorter.SortChunk(chunk.AsMemory());

        Assert.Empty(sorted);
    }

    [Fact]
    public void SortChunk_HandlesSingleLine()
    {
        var chunk = "42. Single Line\n"u8.ToArray();

        var sorted = ChunkSorter.SortChunk(chunk.AsMemory());

        Assert.Single(sorted);
        Assert.Equal("42. Single Line", sorted[0].ToString());
    }

    [Fact]
    public void SortChunk_HandlesLineWithoutTrailingNewline()
    {
        var chunk = "42. No newline"u8.ToArray();

        var sorted = ChunkSorter.SortChunk(chunk.AsMemory());

        Assert.Single(sorted);
        Assert.Equal("42. No newline", sorted[0].ToString());
    }

    [Fact]
    public async Task WriteChunkAsync_WritesAllLines()
    {
        var chunk = """
            2. B
            1. A

            """u8.ToArray();
        var sorted = ChunkSorter.SortChunk(chunk.AsMemory());

        var tempFile = Path.GetTempFileName();
        try
        {
            await ChunkSorter.WriteChunkAsync(sorted, tempFile);

            var lines = await File.ReadAllLinesAsync(tempFile);
            Assert.Equal(2, lines.Length);
            Assert.Equal("1. A", lines[0]);
            Assert.Equal("2. B", lines[1]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
