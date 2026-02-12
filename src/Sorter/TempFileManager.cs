using System.Collections.Concurrent;

namespace FileSorting.Sorter;

public sealed class TempFileManager : IDisposable
{
    private readonly bool _shouldCleanupDir;
    private readonly string _tempDir;
    private readonly ConcurrentBag<string> _tempFiles = [];
    private int _nextChunkId;

    public TempFileManager(string? tempDir = null)
    {
        _shouldCleanupDir = tempDir != null;
        _tempDir = tempDir ?? Path.Combine(Path.GetTempPath(), $"filesort_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public string CreateChunkFile()
    {
        var id = Interlocked.Increment(ref _nextChunkId);
        var path = Path.Combine(_tempDir, $"chunk_{id:D6}.tmp");
        _tempFiles.Add(path);
        return path;
    }

    public string CreateMergeFile(int pass, int index)
    {
        var path = Path.Combine(_tempDir, $"merge_p{pass}_i{index:D6}.tmp");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
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
                // ok
            }
        }

        if (_shouldCleanupDir)
        {
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // ok
            }
        }
    }
}
