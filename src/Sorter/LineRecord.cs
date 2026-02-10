using System.Buffers.Text;
using System.Text;

namespace FileSorting.Sorter;

public readonly struct ParsedLine
{
    public readonly Memory<byte> Buffer;
    public readonly int NumberStart;
    public readonly int NumberLength;
    public readonly int StringStart;
    public readonly int StringLength;

    public ParsedLine(
        Memory<byte> buffer,
        int numberStart,
        int numberLength,
        int stringStart,
        int stringLength)
    {
        Buffer = buffer;
        NumberStart = numberStart;
        NumberLength = numberLength;
        StringStart = stringStart;
        StringLength = stringLength;
    }

    public ReadOnlySpan<byte> NumberPart =>
        Buffer.Span.Slice(NumberStart, NumberLength);
    public ReadOnlySpan<byte> StringPart =>
        Buffer.Span.Slice(StringStart, StringLength);

    public long GetNumber()
    {
        Utf8Parser.TryParse(NumberPart, out long value, out _);
        return value;
    }

    public string GetString() =>
        Encoding.UTF8.GetString(StringPart);

    public override string ToString() =>
        Encoding.UTF8.GetString(Buffer.Span);
}

public static class ParsedLineComparer
{
    public static int Compare(in ParsedLine a, in ParsedLine b)
    {
        var cmp = a.StringPart.SequenceCompareTo(b.StringPart);
        if (cmp != 0) return cmp;

        Utf8Parser.TryParse(a.NumberPart, out long numA, out _);
        Utf8Parser.TryParse(b.NumberPart, out long numB, out _);
        return numA.CompareTo(numB);
    }
}

public sealed class ParsedLineComparerWrapper : IComparer<ParsedLine>
{
    public static readonly ParsedLineComparerWrapper Instance = new();

    private ParsedLineComparerWrapper() { }

    public int Compare(ParsedLine x, ParsedLine y) =>
        ParsedLineComparer.Compare(in x, in y);
}
