using System.Buffers;
using System.IO.Pipelines;

namespace FileSorting.Sorter;

/// <summary>
/// Reads chunks from a file using PipeReader, ensuring proper line boundaries.
/// </summary>
public sealed class ChunkReader : IDisposable
{
    private readonly FileStream _stream;
    private readonly PipeReader _reader;
    private bool _disposed;
    private bool _completed;

    public ChunkReader(string filePath)
    {
        _stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        _reader = PipeReader.Create(_stream);
    }

    public bool IsCompleted => _completed;
    public long Position => _stream.Position;
    public long Length => _stream.Length;

    /// <summary>
    /// Reads a chunk of data up to maxSize, ensuring it ends at a newline boundary.
    /// Returns null when there's no more data.
    /// </summary>
    public async Task<Memory<byte>?> ReadChunkAsync(int maxSize, CancellationToken cancellationToken = default)
    {
        if (_completed) return null;

        var totalBuffer = new ArrayBufferWriter<byte>(maxSize);
        var foundCompleteChunk = false;

        while (!foundCompleteChunk && totalBuffer.WrittenCount < maxSize)
        {
            var result = await _reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            if (buffer.IsEmpty && result.IsCompleted)
            {
                _completed = true;
                if (totalBuffer.WrittenCount == 0)
                    return null;
                break;
            }

            // Find the last newline in the buffer
            var remaining = maxSize - totalBuffer.WrittenCount;
            var toExamine = buffer.Length > remaining ? buffer.Slice(0, remaining) : buffer;

            var lastNewline = FindLastNewline(toExamine);

            if (lastNewline >= 0)
            {
                // Found a newline - consume up to and including it
                var toConsume = toExamine.Slice(0, lastNewline + 1);
                CopyToBuffer(toConsume, totalBuffer);
                _reader.AdvanceTo(toConsume.End);
                foundCompleteChunk = true;
            }
            else if (result.IsCompleted)
            {
                // End of file - consume everything
                CopyToBuffer(toExamine, totalBuffer);
                _reader.AdvanceTo(toExamine.End);
                _completed = true;
                break;
            }
            else if (toExamine.Length >= maxSize)
            {
                // Buffer is full but no newline found - consume what we have
                // This handles very long lines
                CopyToBuffer(toExamine, totalBuffer);
                _reader.AdvanceTo(toExamine.End);
                break;
            }
            else
            {
                // Need more data to find a newline
                _reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }

        if (totalBuffer.WrittenCount == 0)
            return null;

        return totalBuffer.WrittenMemory.ToArray();
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

    private static void CopyToBuffer(ReadOnlySequence<byte> source, ArrayBufferWriter<byte> destination)
    {
        foreach (var segment in source)
            destination.Write(segment.Span);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Complete();
        _stream.Dispose();
    }
}
