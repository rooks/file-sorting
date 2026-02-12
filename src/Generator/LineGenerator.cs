using System.Buffers.Text;

namespace FileSorting.Generator;

/// <summary>
/// Generates lines in format "Number. String".
/// Each instance should be used by a single thread only.
/// </summary>
public sealed class LineGenerator
{
    private readonly DictionaryStringPool _stringPool;
    private readonly int _maxNumber;
    private readonly Random _random;

    public LineGenerator(
        DictionaryStringPool stringPool,
        int maxNumber = 1_000_000_000,
        int? seed = null)
    {
        _stringPool = stringPool;
        _maxNumber = maxNumber;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public int WriteLine(Span<byte> buffer)
    {
        // perf: single RNG call for number & stringIndex
        var rng = _random.NextInt64();
        var number = (int)((rng & 0x7FFFFFFF) % _maxNumber) + 1;
        var stringIndex = (int)((rng >> 32) % _stringPool.Count);

        var stringBytes = _stringPool.GetString(stringIndex);
        var written = 0;

        // number
        if (!Utf8Formatter.TryFormat(number, buffer[written..], out var numberBytesWritten))
            throw new InvalidOperationException("Buffer too small for number");
        written += numberBytesWritten;

        // ". "
        if (buffer.Length - written < 2)
            throw new InvalidOperationException("Buffer too small for separator");
        buffer[written++] = (byte)'.';
        buffer[written++] = (byte)' ';

        // string
        if (buffer.Length - written < stringBytes.Length)
            throw new InvalidOperationException("Buffer too small for string");
        stringBytes.CopyTo(buffer[written..]);
        written += stringBytes.Length;

        // newline
        if (buffer.Length - written < 1)
            throw new InvalidOperationException("Buffer too small for newline");
        buffer[written++] = (byte)'\n';

        return written;
    }
}
