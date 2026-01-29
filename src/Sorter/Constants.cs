namespace FileSorting.Sorter;

public static class Constants
{
    public const int MergeWidth = 16; // Number of files to merge at once
    public const double MemoryUsageRatio = 0.6; // Use 60% of available RAM
    public const int MinChunkSize = 64 * 1024 * 1024; // 64MB minimum chunk
    public const int MaxChunkSize = 512 * 1024 * 1024; // 512MB maximum chunk
}
