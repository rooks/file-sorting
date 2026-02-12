using System.Buffers;

namespace FileSorting.Sorter;

/// <summary>
/// Sorts lines within a chunk using parallel processing.
/// </summary>
public static class ChunkSorter
{
    private const int WriteBufferSize = 64 * 1024; // 64KB write buffer
    private const int FileStreamBufferSize = 16 * 1024 * 1024; // 16MB FileStream buffer
    private const byte NewLineCh = (byte)'\n';
    private static readonly byte[] NewLine = [NewLineCh];

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
        var start = 0;

        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] != NewLineCh) continue;

            var lineLength = i - start;
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

        // Handle last line without newline
        if (start < span.Length)
        {
            var lineMemory = chunk[start..];
            if (LineParser.TryParse(lineMemory, out var parsed))
            {
                lines.Add(parsed);
            }
        }

        return lines;
    }

    /// <summary>
    /// Writes sorted lines to a file using binary writes (no string allocations).
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
            bufferSize: FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var writeBuffer = ArrayPool<byte>.Shared.Rent(WriteBufferSize);
        try
        {
            var bufferPos = 0;

            foreach (var line in sortedLines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var lineLength = line.Buffer.Length;
                var requiredSize = lineLength + 1; // +1 for newline

                // Flush buffer if this line won't fit
                if (bufferPos + requiredSize > writeBuffer.Length)
                {
                    if (bufferPos > 0)
                    {
                        await stream.WriteAsync(writeBuffer.AsMemory(0, bufferPos), cancellationToken);
                        bufferPos = 0;
                    }

                    // If single line is larger than buffer, write directly
                    if (requiredSize > writeBuffer.Length)
                    {
                        await stream.WriteAsync(line.Buffer, cancellationToken);
                        await stream.WriteAsync(NewLine, cancellationToken);
                        continue;
                    }
                }

                // Copy line to buffer (use Span inside synchronous block)
                line.Buffer.Span.CopyTo(writeBuffer.AsSpan(bufferPos));
                bufferPos += lineLength;
                writeBuffer[bufferPos++] = (byte)'\n';
            }

            // Flush remaining data
            if (bufferPos > 0)
            {
                await stream.WriteAsync(writeBuffer.AsMemory(0, bufferPos), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(writeBuffer);
        }
    }
}
