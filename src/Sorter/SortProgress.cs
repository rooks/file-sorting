namespace FileSorting.Sorter;

public enum SortPhase
{
    Chunking,
    Sorting,  // In-memory sort phase (read + sort)
    Merging,
    Completed
}

public readonly struct SortProgress(SortPhase phase, long current, long total)
{
    public SortPhase Phase { get; } = phase;
    public long Current { get; } = current;
    public long Total { get; } = total;
}
