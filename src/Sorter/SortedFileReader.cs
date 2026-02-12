using System.Buffers;
using System.IO.Pipelines;

namespace FileSorting.Sorter;

/// <summary>
/// Reads lines from a sorted chunk file for merging using PipeReader for efficiency.
/// </summary>
public sealed class SortedFileReader : IDisposable
{
    private const int FileStreamBufferSize = 256 * 1024; // 256KB read buffer
    private const int MinLineBufferSize = 256;
    private readonly FileStream _stream;
    private readonly PipeReader _reader;
    private readonly int _fileIndex;
    private byte[]? _lineBuffer;
    private bool _disposed;
    private bool _eof;

    public SortedFileReader(string path, int fileIndex)
    {
        _stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: FileStreamBufferSize,
            FileOptions.SequentialScan);
        _reader = PipeReader.Create(_stream);
        _fileIndex = fileIndex;
    }

    /// <summary>
    /// Reads the next line and creates a MergeEntry.
    /// Returns null if at end of file.
    /// </summary>
    public async Task<MergeEntry?> ReadNextAsync(CancellationToken ct = default)
    {
        if (_eof) return null;

        while (true)
        {
            var result = await _reader.ReadAsync(ct);
            var buffer = result.Buffer;

            if (buffer.IsEmpty && result.IsCompleted)
            {
                _eof = true;
                return null;
            }

            // Find the newline
            var newlinePos = FindNewline(buffer);

            if (newlinePos != null)
            {
                // Extract the line (without newline)
                var lineSequence = buffer.Slice(0, newlinePos.Value);
                var lineLength = (int)lineSequence.Length;

                var lineMemory = CopyToLineBuffer(lineSequence, lineLength);

                // Advance past the newline
                _reader.AdvanceTo(buffer.GetPosition(1, newlinePos.Value));

                // Try to parse
                if (LineParser.TryParse(lineMemory, out var parsed))
                {
                    return new MergeEntry(parsed, _fileIndex, lineMemory);
                }

                // Invalid line, continue to next line.
                continue;
            }

            if (result.IsCompleted)
            {
                // Last line without newline
                if (buffer.Length > 0)
                {
                    var lineLength = (int)buffer.Length;
                    var lineMemory = CopyToLineBuffer(buffer, lineLength);

                    _reader.AdvanceTo(buffer.End);
                    _eof = true;

                    if (LineParser.TryParse(lineMemory, out var parsed))
                    {
                        return new MergeEntry(parsed, _fileIndex, lineMemory);
                    }
                }

                _eof = true;
                return null;
            }

            // Need more data
            _reader.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    private static SequencePosition? FindNewline(ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        if (reader.TryAdvanceTo((byte)'\n', advancePastDelimiter: false))
        {
            return reader.Position;
        }
        return null;
    }

    private Memory<byte> CopyToLineBuffer(
        ReadOnlySequence<byte> lineSequence,
        int lineLength)
    {
        EnsureLineBufferCapacity(lineLength);
        lineSequence.CopyTo(_lineBuffer!);
        return _lineBuffer!.AsMemory(0, lineLength);
    }

    private void EnsureLineBufferCapacity(int requiredLength)
    {
        if (_lineBuffer != null && _lineBuffer.Length >= requiredLength)
            return;

        var newBufferLength = Math.Max(requiredLength, MinLineBufferSize);
        var newBuffer = ArrayPool<byte>.Shared.Rent(newBufferLength);

        if (_lineBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_lineBuffer);
        }

        _lineBuffer = newBuffer;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_lineBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_lineBuffer);
            _lineBuffer = null;
        }

        _reader.Complete();
        _stream.Dispose();
    }
}
