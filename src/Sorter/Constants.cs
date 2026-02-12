namespace FileSorting.Sorter;

public static class Constants
{
    public const int MinMergeWidth = 8; // Lower bound for adaptive merge width
    public const int MaxMergeWidth = 64; // Upper bound to limit open file handles
    public const double MemoryUsageRatio = 0.6; // Use 60% of available RAM
    public const int MinChunkSize = 64 * 1024 * 1024; // 64MB minimum chunk
    public const int MaxChunkSize = 1024 * 1024 * 1024; // 1GB maximum chunk
    public const int ChunkReadBufferSize = 4 * 1024 * 1024; // 4MB read buffer
    public const int WriteBufferSize = 4 * 1024 * 1024; // 4MB write buffer
    public const int BoundaryProbeBufferSize = 64 * 1024; // 64KB for boundary probing
    public const int EstimatedBytesPerLine = 32; // Used to pre-size line collections
    public const byte NewLineCh = (byte)'\n';
}
