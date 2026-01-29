using System.Buffers.Text;

namespace FileSorting.Sorter;

/// <summary>
/// Represents a parsed line in format "Number. String" with zero-allocation access to parts.
/// Stores offsets into a byte buffer rather than allocating strings.
/// </summary>
public readonly struct ParsedLine
{
    public readonly Memory<byte> Buffer;
    public readonly int NumberStart;
    public readonly int NumberLength;
    public readonly int StringStart;
    public readonly int StringLength;

    public ParsedLine(Memory<byte> buffer, int numberStart, int numberLength, int stringStart, int stringLength)
    {
        Buffer = buffer;
        NumberStart = numberStart;
        NumberLength = numberLength;
        StringStart = stringStart;
        StringLength = stringLength;
    }

    public ReadOnlySpan<byte> NumberPart => Buffer.Span.Slice(NumberStart, NumberLength);
    public ReadOnlySpan<byte> StringPart => Buffer.Span.Slice(StringStart, StringLength);

    public long GetNumber()
    {
        Utf8Parser.TryParse(NumberPart, out long value, out _);
        return value;
    }

    public string GetString() => System.Text.Encoding.UTF8.GetString(StringPart);

    public override string ToString() => System.Text.Encoding.UTF8.GetString(Buffer.Span);
}

/// <summary>
/// Comparer for ParsedLine that sorts by string (alphabetically), then by number (ascending).
/// </summary>
public static class ParsedLineComparer
{
    public static int Compare(in ParsedLine a, in ParsedLine b)
    {
        int cmp = a.StringPart.SequenceCompareTo(b.StringPart);
        if (cmp != 0) return cmp;

        // Parse numbers only when strings are equal
        Utf8Parser.TryParse(a.NumberPart, out long numA, out _);
        Utf8Parser.TryParse(b.NumberPart, out long numB, out _);
        return numA.CompareTo(numB);
    }
}

/// <summary>
/// IComparer wrapper for use with sorting APIs
/// </summary>
public sealed class ParsedLineComparerWrapper : IComparer<ParsedLine>
{
    public static readonly ParsedLineComparerWrapper Instance = new();

    private ParsedLineComparerWrapper() { }

    public int Compare(ParsedLine x, ParsedLine y) => ParsedLineComparer.Compare(in x, in y);
}
