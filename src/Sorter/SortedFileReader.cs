using System.Buffers;
using FileSorting.Shared;

namespace FileSorting.Sorter;

/// <summary>
/// Reads lines from a sorted chunk file for merging.
/// </summary>
public sealed class SortedFileReader : IDisposable
{
    private readonly StreamReader _reader;
    private readonly int _fileIndex;
    private byte[]? _currentLineBuffer;
    private bool _disposed;
    private bool _eof;

    public SortedFileReader(string path, int fileIndex)
    {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        _reader = new StreamReader(stream, System.Text.Encoding.UTF8, bufferSize: 64 * 1024);
        _fileIndex = fileIndex;
    }

    public int FileIndex => _fileIndex;
    public bool EndOfFile => _eof;

    /// <summary>
    /// Reads the next line and creates a MergeEntry.
    /// Returns null if at end of file.
    /// </summary>
    public async Task<MergeEntry?> ReadNextAsync(CancellationToken cancellationToken = default)
    {
        if (_eof) return null;

        var line = await _reader.ReadLineAsync(cancellationToken);
        if (line == null)
        {
            _eof = true;
            return null;
        }

        // Convert to bytes for parsing
        _currentLineBuffer = System.Text.Encoding.UTF8.GetBytes(line);
        var memory = _currentLineBuffer.AsMemory();

        if (!LineParser.TryParse(memory, out var parsed))
        {
            // Skip invalid lines
            return await ReadNextAsync(cancellationToken);
        }

        return new MergeEntry(parsed, _fileIndex, memory);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Dispose();
    }
}
