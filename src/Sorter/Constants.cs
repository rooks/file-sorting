namespace FileSorting.Sorter;

public static class Constants
{
    public const int MergeWidth = 16; // Number of files to merge at once
    public const double MemoryUsageRatio = 0.6; // Use 60% of available RAM
    public const int MinChunkSize = 64 * 1024 * 1024; // 64MB minimum chunk
    public const int MaxChunkSize = 1536 * 1024 * 1024; // 1.5GB maximum chunk
    public const int ChunkReadBufferSize = 4 * 1024 * 1024; // 4MB read buffer
    public const int WriteBufferSize = 4 * 1024 * 1024; // 4MB write buffer
}
