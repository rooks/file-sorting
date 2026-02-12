using System.Text;

namespace FileSorting.Sorter;

public readonly struct ParsedLine
{
    public readonly Memory<byte> Buffer;
    public readonly int NumberStart;
    public readonly int NumberLength;
    public readonly int StringStart;
    public readonly int StringLength;
    public readonly long NumberValue;

    public ParsedLine(
        Memory<byte> buffer,
        int numberStart,
        int numberLength,
        int stringStart,
        int stringLength,
        long numberValue)
    {
        Buffer = buffer;
        NumberStart = numberStart;
        NumberLength = numberLength;
        StringStart = stringStart;
        StringLength = stringLength;
        NumberValue = numberValue;
    }

    public ReadOnlySpan<byte> NumberPart =>
        Buffer.Span.Slice(NumberStart, NumberLength);
    public ReadOnlySpan<byte> StringPart =>
        Buffer.Span.Slice(StringStart, StringLength);

    public long GetNumber() => NumberValue;

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
        return a.NumberValue.CompareTo(b.NumberValue);
    }
}

public sealed class ParsedLineComparerWrapper : IComparer<ParsedLine>
{
    public static readonly ParsedLineComparerWrapper Instance = new();

    private ParsedLineComparerWrapper() { }

    public int Compare(ParsedLine x, ParsedLine y) =>
        ParsedLineComparer.Compare(in x, in y);
}
