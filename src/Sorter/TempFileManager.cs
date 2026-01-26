namespace FileSorting.Sorter;

/// <summary>
/// Manages temporary files for the external merge sort.
/// </summary>
public sealed class TempFileManager : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _tempFiles = [];
    private int _nextChunkId;
    private bool _disposed;

    public TempFileManager(string? tempDir = null)
    {
        _tempDir = tempDir ?? Path.Combine(Path.GetTempPath(), $"filesort_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public string CreateChunkFile()
    {
        var path = Path.Combine(_tempDir, $"chunk_{Interlocked.Increment(ref _nextChunkId):D6}.tmp");
        _tempFiles.Add(path);
        return path;
    }

    public string CreateMergeFile(int pass, int index)
    {
        var path = Path.Combine(_tempDir, $"merge_p{pass}_i{index:D6}.tmp");
        _tempFiles.Add(path);
        return path;
    }

    public IReadOnlyList<string> TempFiles => _tempFiles;

    public void Cleanup()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch
            {
                // Best effort cleanup
            }
        }
        _tempFiles.Clear();

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cleanup();
    }
}
