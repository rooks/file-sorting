namespace FileSorting.Sorter;

/// <summary>
/// Sorts lines within a chunk using parallel processing.
/// </summary>
public static class ChunkSorter
{
    private static readonly System.Text.UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Parses and sorts lines in the chunk, returning them ready for writing.
    /// </summary>
    public static List<ParsedLine> SortChunk(Memory<byte> chunk)
    {
        var lines = ParseLines(chunk);
        lines.Sort(ParsedLineComparerWrapper.Instance);
        return lines;
    }

    /// <summary>
    /// Parses lines from the chunk buffer.
    /// Each ParsedLine references the original buffer memory.
    /// </summary>
    private static List<ParsedLine> ParseLines(Memory<byte> chunk)
    {
        var lines = new List<ParsedLine>();
        var span = chunk.Span;
        int start = 0;

        for (int i = 0; i < span.Length; i++)
        {
            if (span[i] == (byte)'\n')
            {
                int lineLength = i - start;
                if (lineLength > 0)
                {
                    var lineMemory = chunk.Slice(start, lineLength);
                    if (LineParser.TryParse(lineMemory, out var parsed))
                    {
                        lines.Add(parsed);
                    }
                }
                start = i + 1;
            }
        }

        // Handle last line without newline
        if (start < span.Length)
        {
            var lineMemory = chunk.Slice(start);
            if (LineParser.TryParse(lineMemory, out var parsed))
            {
                lines.Add(parsed);
            }
        }

        return lines;
    }

    /// <summary>
    /// Writes sorted lines to a file.
    /// </summary>
    public static async Task WriteChunkAsync(
        IEnumerable<ParsedLine> sortedLines,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var writer = new StreamWriter(stream, Utf8NoBom, bufferSize: 64 * 1024);

        foreach (var line in sortedLines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(line.ToString());
        }
    }
}
