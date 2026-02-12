using System.Runtime.InteropServices;

namespace FileSorting.Sorter;

/// <summary>
/// Three-way quicksort optimized for data with many duplicate strings.
/// Partitions by string equality using Dutch National Flag, then sorts
/// equal-string groups by number only (much cheaper than full comparison).
/// </summary>
public static class ThreeWaySort
{
    private const int InsertionSortThreshold = 32;
    private const int MaxRecursionDepth = 64;

    public static void Sort(List<ParsedLine> lines)
    {
        if (lines.Count <= 1) return;
        var span = CollectionsMarshal.AsSpan(lines);
        SortByStringThenNumber(span, 0);
    }

    private static void SortByStringThenNumber(Span<ParsedLine> items, int depth)
    {
        while (true)
        {
            if (items.Length <= 1) return;

            if (items.Length <= InsertionSortThreshold)
            {
                InsertionSort(items);
                return;
            }

            if (depth >= MaxRecursionDepth)
            {
                items.Sort(ParsedLineComparerWrapper.Instance);
                return;
            }

            // Median-of-three pivot selection, places median at items[0]
            MedianOfThree(items);

            // Save pivot string span — references the underlying chunk buffer
            // which remains valid throughout partitioning (only structs move, not buffers)
            var pivotStringStart = items[0].StringStart;
            var pivotStringLength = items[0].StringLength;
            var pivotBuffer = items[0].Buffer;

            // Dutch National Flag partition on string part
            // Invariant: [0..lt) < pivot, [lt..i) == pivot, (gt..end] > pivot
            var lt = 0;
            var i = 1;
            var gt = items.Length - 1;

            while (i <= gt)
            {
                var cmp = items[i].StringPart.SequenceCompareTo(
                    pivotBuffer.Span.Slice(pivotStringStart, pivotStringLength));
                if (cmp < 0)
                {
                    (items[lt], items[i]) = (items[i], items[lt]);
                    lt++;
                    i++;
                }
                else if (cmp > 0)
                {
                    (items[i], items[gt]) = (items[gt], items[i]);
                    gt--;
                }
                else
                {
                    i++;
                }
            }

            // Sort equal-string partition by number only (much cheaper)
            var equalCount = gt - lt + 1;
            if (equalCount > 1)
                SortByNumberOnly(items.Slice(lt, equalCount));

            // Recurse on smaller partition, iterate on larger (tail-call optimization)
            var leftSize = lt;
            var rightSize = items.Length - gt - 1;

            if (leftSize < rightSize)
            {
                SortByStringThenNumber(items[..lt], depth + 1);
                items = items[(gt + 1)..];
            }
            else
            {
                if (gt + 1 < items.Length)
                    SortByStringThenNumber(items[(gt + 1)..], depth + 1);
                items = items[..lt];
            }

            depth++;
        }
    }

    private static void SortByNumberOnly(Span<ParsedLine> items)
    {
        if (items.Length <= InsertionSortThreshold)
        {
            InsertionSortByNumber(items);
            return;
        }

        QuickSortByNumber(items, 0);
    }

    private static void QuickSortByNumber(Span<ParsedLine> items, int depth)
    {
        while (true)
        {
            if (items.Length <= InsertionSortThreshold)
            {
                InsertionSortByNumber(items);
                return;
            }

            if (depth >= MaxRecursionDepth)
            {
                // Fallback — shouldn't happen in practice for number sorting
                InsertionSortByNumber(items);
                return;
            }

            // Median-of-three for number pivot
            MedianOfThreeByNumber(items);
            var pivot = items[0].NumberValue;

            // Hoare-like partition
            var lo = 1;
            var hi = items.Length - 1;
            while (lo <= hi)
            {
                while (lo <= hi && items[lo].NumberValue < pivot) lo++;
                while (lo <= hi && items[hi].NumberValue > pivot) hi--;
                if (lo <= hi)
                {
                    (items[lo], items[hi]) = (items[hi], items[lo]);
                    lo++;
                    hi--;
                }
            }

            // Place pivot
            (items[0], items[hi]) = (items[hi], items[0]);

            // Recurse on smaller, iterate on larger
            if (hi < items.Length - lo)
            {
                QuickSortByNumber(items[..hi], depth + 1);
                items = items[lo..];
            }
            else
            {
                QuickSortByNumber(items[lo..], depth + 1);
                items = items[..hi];
            }

            depth++;
        }
    }

    private static void InsertionSort(Span<ParsedLine> items)
    {
        for (var i = 1; i < items.Length; i++)
        {
            var key = items[i];
            var j = i - 1;
            while (j >= 0 && ParsedLineComparer.Compare(in key, in items[j]) < 0)
            {
                items[j + 1] = items[j];
                j--;
            }

            items[j + 1] = key;
        }
    }

    private static void InsertionSortByNumber(Span<ParsedLine> items)
    {
        for (var i = 1; i < items.Length; i++)
        {
            var key = items[i];
            var keyNum = key.NumberValue;
            var j = i - 1;
            while (j >= 0 && items[j].NumberValue > keyNum)
            {
                items[j + 1] = items[j];
                j--;
            }

            items[j + 1] = key;
        }
    }

    private static void MedianOfThree(Span<ParsedLine> items)
    {
        var mid = items.Length / 2;
        var last = items.Length - 1;

        if (items[0].StringPart.SequenceCompareTo(items[mid].StringPart) > 0)
            (items[0], items[mid]) = (items[mid], items[0]);
        if (items[0].StringPart.SequenceCompareTo(items[last].StringPart) > 0)
            (items[0], items[last]) = (items[last], items[0]);
        if (items[mid].StringPart.SequenceCompareTo(items[last].StringPart) > 0)
            (items[mid], items[last]) = (items[last], items[mid]);

        // Place median at position 0 as pivot
        (items[0], items[mid]) = (items[mid], items[0]);
    }

    private static void MedianOfThreeByNumber(Span<ParsedLine> items)
    {
        var mid = items.Length / 2;
        var last = items.Length - 1;

        if (items[0].NumberValue > items[mid].NumberValue)
            (items[0], items[mid]) = (items[mid], items[0]);
        if (items[0].NumberValue > items[last].NumberValue)
            (items[0], items[last]) = (items[last], items[0]);
        if (items[mid].NumberValue > items[last].NumberValue)
            (items[mid], items[last]) = (items[last], items[mid]);

        (items[0], items[mid]) = (items[mid], items[0]);
    }
}
