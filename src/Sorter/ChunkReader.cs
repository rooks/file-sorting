using System.Buffers;
using System.IO.Pipelines;

namespace FileSorting.Sorter;

/// <summary>
/// Reads chunks from a file using PipeReader, ensuring proper line boundaries.
/// Returns rented buffers from ArrayPool that consumers must return after use.
/// </summary>
public sealed class ChunkReader : IDisposable
{
    private const int FileStreamBufferSize = 4 * 1024 * 1024; // 4MB read buffer
    private readonly FileStream _stream;
    private readonly PipeReader _reader;
    private bool _disposed;

    public ChunkReader(string filePath)
    {
        _stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: FileStreamBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        _reader = PipeReader.Create(_stream);
    }

    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Reads a chunk of data up to maxSize, ensuring it ends at a newline boundary.
    /// Returns null when there's no more data.
    /// IMPORTANT: Caller must return the buffer to ArrayPool&lt;byte&gt;.Shared when done!
    /// </summary>
    public async Task<(byte[] Buffer, int Length)?> ReadChunkAsync(
        int maxSize,
        CancellationToken ct = default)
    {
        if (IsCompleted) return null;

        // Rent a buffer from the pool for this chunk
        var chunkBuffer = ArrayPool<byte>.Shared.Rent(maxSize);
        var totalWritten = 0;
        var foundCompleteChunk = false;

        while (!foundCompleteChunk && totalWritten < maxSize)
        {
            var result = await _reader.ReadAsync(ct);
            var buffer = result.Buffer;

            if (buffer.IsEmpty && result.IsCompleted)
            {
                IsCompleted = true;
                if (totalWritten == 0)
                {
                    ArrayPool<byte>.Shared.Return(chunkBuffer);
                    return null;
                }
                break;
            }

            // Find the last newline in the buffer
            var remaining = maxSize - totalWritten;
            var toExamine = buffer.Length > remaining ? buffer.Slice(0, remaining) : buffer;

            var lastNewline = FindLastNewline(toExamine);

            if (lastNewline >= 0)
            {
                // Found a newline - consume up to and including it
                var toConsume = toExamine.Slice(0, lastNewline + 1);
                CopyToBuffer(toConsume, chunkBuffer, ref totalWritten);
                _reader.AdvanceTo(toConsume.End);
                foundCompleteChunk = true;
            }
            else if (result.IsCompleted)
            {
                // End of file - consume everything
                CopyToBuffer(toExamine, chunkBuffer, ref totalWritten);
                _reader.AdvanceTo(toExamine.End);
                IsCompleted = true;
                break;
            }
            else if (toExamine.Length >= remaining)
            {
                // Buffer is full but no newline found - consume what we have
                // This handles very long lines
                CopyToBuffer(toExamine, chunkBuffer, ref totalWritten);
                _reader.AdvanceTo(toExamine.End);
                break;
            }
            else
            {
                // Need more data to find a newline
                _reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }

        if (totalWritten == 0)
        {
            ArrayPool<byte>.Shared.Return(chunkBuffer);
            return null;
        }

        return (chunkBuffer, totalWritten);
    }

    private static long FindLastNewline(ReadOnlySequence<byte> buffer)
    {
        var lastNewlinePos = -1L;
        var segmentStart = 0L;

        foreach (var segment in buffer)
        {
            var span = segment.Span;
            var lastNewlineInSegment = span.LastIndexOf((byte)'\n');
            if (lastNewlineInSegment >= 0)
            {
                lastNewlinePos = segmentStart + lastNewlineInSegment;
            }
            segmentStart += segment.Length;
        }

        return lastNewlinePos;
    }

    private static void CopyToBuffer(ReadOnlySequence<byte> source, byte[] destination, ref int offset)
    {
        foreach (var segment in source)
        {
            segment.Span.CopyTo(destination.AsSpan(offset));
            offset += segment.Length;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Complete();
        _stream.Dispose();
    }
}
