using System.Buffers;
using K4os.Compression.LZ4.Streams;

namespace FileSorting.Sorter;

/// <summary>
/// Sorts lines within a chunk using parallel processing.
/// </summary>
public static class ChunkSorter
{
    private const int FileStreamBufferSize = 16 * 1024 * 1024; // 16MB FileStream buffer
    private static readonly byte[] NewLine = [Constants.NewLineCh];

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
        var estimatedLineCount = Math.Max(16, chunk.Length / Constants.EstimatedBytesPerLine);
        var lines = new List<ParsedLine>(estimatedLineCount);
        var span = chunk.Span;
        var start = 0;

        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] != Constants.NewLineCh) continue;

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
    public static void WriteChunk(
        IEnumerable<ParsedLine> sortedLines,
        string outputPath,
        CancellationToken ct = default)
    {
        using var fileStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: FileStreamBufferSize,
            FileOptions.SequentialScan);
        using var stream = LZ4Stream.Encode(fileStream, leaveOpen: true);

        var writeBuffer = ArrayPool<byte>.Shared.Rent(Constants.WriteBufferSize);
        try
        {
            var bufferPos = 0;
            foreach (var line in sortedLines)
            {
                ct.ThrowIfCancellationRequested();

                var lineLength = line.Buffer.Length;
                var requiredSize = lineLength + 1; // +1 for newline

                // Flush buffer if this line won't fit
                if (bufferPos + requiredSize > writeBuffer.Length)
                {
                    if (bufferPos > 0)
                    {
                        stream.Write(writeBuffer, 0, bufferPos);
                        bufferPos = 0;
                    }

                    // If single line is larger than buffer, write directly
                    if (requiredSize > writeBuffer.Length)
                    {
                        stream.Write(line.Buffer.Span);
                        stream.Write(NewLine);
                        continue;
                    }
                }

                // Copy line to buffer
                line.Buffer.Span.CopyTo(writeBuffer.AsSpan(bufferPos));
                bufferPos += lineLength;
                writeBuffer[bufferPos++] = Constants.NewLineCh;
            }

            // Flush remaining data
            if (bufferPos > 0)
            {
                stream.Write(writeBuffer, 0, bufferPos);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(writeBuffer);
        }
    }
}
