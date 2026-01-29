namespace FileSorting.Sorter;

public sealed class TempFileManager : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _tempFiles = [];
    private int _nextChunkId;

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
                // best effort cleanup
            }
        }

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best effort cleanup
        }
    }
}
