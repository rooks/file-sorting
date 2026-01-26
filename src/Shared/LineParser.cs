namespace FileSorting.Shared;

/// <summary>
/// Parses lines in format "Number. String" from byte buffers.
/// </summary>
public static class LineParser
{
    private static ReadOnlySpan<byte> Separator => ". "u8;

    /// <summary>
    /// Parses a line from a byte buffer.
    /// </summary>
    /// <param name="buffer">The buffer containing the line data</param>
    /// <returns>A ParsedLine with offsets into the buffer</returns>
    public static ParsedLine Parse(Memory<byte> buffer)
    {
        var span = buffer.Span;
        int separatorIndex = span.IndexOf(Separator);

        if (separatorIndex < 0)
        {
            throw new FormatException($"Invalid line format: separator '. ' not found");
        }

        return new ParsedLine(
            buffer,
            numberStart: 0,
            numberLength: separatorIndex,
            stringStart: separatorIndex + 2,
            stringLength: span.Length - separatorIndex - 2
        );
    }

    /// <summary>
    /// Tries to parse a line from a byte buffer.
    /// </summary>
    public static bool TryParse(Memory<byte> buffer, out ParsedLine result)
    {
        var span = buffer.Span;
        int separatorIndex = span.IndexOf(Separator);

        if (separatorIndex < 0)
        {
            result = default;
            return false;
        }

        result = new ParsedLine(
            buffer,
            numberStart: 0,
            numberLength: separatorIndex,
            stringStart: separatorIndex + 2,
            stringLength: span.Length - separatorIndex - 2
        );
        return true;
    }

    /// <summary>
    /// Finds the next newline in the buffer and returns its index.
    /// </summary>
    public static int FindNewline(ReadOnlySpan<byte> buffer)
    {
        return buffer.IndexOf((byte)'\n');
    }

    /// <summary>
    /// Finds the last newline in the buffer and returns its index.
    /// </summary>
    public static int FindLastNewline(ReadOnlySpan<byte> buffer)
    {
        return buffer.LastIndexOf((byte)'\n');
    }
}
