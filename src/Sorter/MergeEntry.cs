namespace FileSorting.Sorter;

/// <summary>
/// Entry in the merge priority queue, representing a line from a specific file.
/// </summary>
public readonly struct MergeEntry : IComparable<MergeEntry>
{
    public readonly ParsedLine Line;
    public readonly int FileIndex;
    public readonly Memory<byte> LineBuffer;

    public MergeEntry(ParsedLine line, int fileIndex, Memory<byte> lineBuffer)
    {
        Line = line;
        FileIndex = fileIndex;
        LineBuffer = lineBuffer;
    }

    public int CompareTo(MergeEntry other)
    {
        return ParsedLineComparer.Compare(in Line, in other.Line);
    }
}

/// <summary>
/// Comparer for MergeEntry used by PriorityQueue.
/// </summary>
public sealed class MergeEntryComparer : IComparer<MergeEntry>
{
    public static readonly MergeEntryComparer Instance = new();

    private MergeEntryComparer() { }

    public int Compare(MergeEntry x, MergeEntry y) => x.CompareTo(y);
}
