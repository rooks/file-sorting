using System.Buffers;
using System.Buffers.Text;

namespace FileSorting.Generator;

/// <summary>
/// Generates lines in format "Number. String" using zero-allocation techniques.
/// </summary>
public sealed class LineGenerator(
    StringPool stringPool,
    long maxNumber = 1_000_000_000,
    int? seed = null)
{
    private readonly Random _random = seed.HasValue ? new Random(seed.Value) : new Random();

    /// <summary>
    /// Writes a line to the buffer and returns bytes written.
    /// Format: "{number}. {string}\n"
    /// </summary>
    public int WriteLine(Span<byte> buffer)
    {
        var number = _random.NextInt64(1, maxNumber + 1);
        var stringBytes = stringPool.GetString();
        var written = 0;

        // number
        if (!Utf8Formatter.TryFormat(number, buffer[written..], out var numberBytesWritten))
        {
            throw new InvalidOperationException("Buffer too small for number");
        }
        written += numberBytesWritten;

        // ". "
        if (buffer.Length - written < 2)
        {
            throw new InvalidOperationException("Buffer too small for separator");
        }
        buffer[written++] = (byte)'.';
        buffer[written++] = (byte)' ';

        // string
        if (buffer.Length - written < stringBytes.Length)
        {
            throw new InvalidOperationException("Buffer too small for string");
        }
        stringBytes.CopyTo(buffer[written..]);
        written += stringBytes.Length;

        // newline
        if (buffer.Length - written < 1)
        {
            throw new InvalidOperationException("Buffer too small for newline");
        }
        buffer[written++] = (byte)'\n';

        return written;
    }

    /// <summary>
    /// Estimates the average line size in bytes.
    /// </summary>
    public static int EstimateAverageLineSize(int avgStringLength = 25)
    {
        // Number (up to 10 digits) + ". " + string + "\n"
        return 10 + 2 + avgStringLength + 1;
    }
}
