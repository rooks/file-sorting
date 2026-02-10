using System.Buffers;
using System.IO.Pipelines;

namespace FileSorting.Sorter;

/// <summary>
/// Reads lines from a sorted chunk file for merging using PipeReader for efficiency.
/// </summary>
public sealed class SortedFileReader : IDisposable
{
    private const int FileStreamBufferSize = 256 * 1024; // 256KB read buffer
    private readonly FileStream _stream;
    private readonly PipeReader _reader;
    private readonly int _fileIndex;
    private byte[]? _currentLineBuffer;
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
    public async Task<MergeEntry?> ReadNextAsync(CancellationToken cancellationToken = default)
    {
        if (_eof) return null;

        // Return previous line buffer to pool
        ReturnCurrentBuffer();

        while (true)
        {
            var result = await _reader.ReadAsync(cancellationToken);
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

                // Rent buffer from pool and copy line data
                _currentLineBuffer = ArrayPool<byte>.Shared.Rent(lineLength);
                lineSequence.CopyTo(_currentLineBuffer);
                var lineMemory = _currentLineBuffer.AsMemory(0, lineLength);

                // Advance past the newline
                _reader.AdvanceTo(buffer.GetPosition(1, newlinePos.Value));

                // Try to parse
                if (LineParser.TryParse(lineMemory, out var parsed))
                {
                    return new MergeEntry(parsed, _fileIndex, lineMemory);
                }

                // Invalid line, return buffer and try next
                ReturnCurrentBuffer();
                continue;
            }

            if (result.IsCompleted)
            {
                // Last line without newline
                if (buffer.Length > 0)
                {
                    var lineLength = (int)buffer.Length;
                    _currentLineBuffer = ArrayPool<byte>.Shared.Rent(lineLength);
                    buffer.CopyTo(_currentLineBuffer);
                    var lineMemory = _currentLineBuffer.AsMemory(0, lineLength);

                    _reader.AdvanceTo(buffer.End);
                    _eof = true;

                    if (LineParser.TryParse(lineMemory, out var parsed))
                    {
                        return new MergeEntry(parsed, _fileIndex, lineMemory);
                    }

                    ReturnCurrentBuffer();
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

    private void ReturnCurrentBuffer()
    {
        if (_currentLineBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(_currentLineBuffer);
            _currentLineBuffer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReturnCurrentBuffer();
        _reader.Complete();
        _stream.Dispose();
    }
}
