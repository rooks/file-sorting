namespace FileSorting.Generator;

/// <summary>
/// Manages a pool of strings with configurable duplicate ratio.
/// When duplicateRatio is 0.3, 30% of strings returned will be duplicates.
/// </summary>
public sealed class StringPool
{
    private const int MaxPoolSize = 100_000;

    private readonly List<byte[]> _pool = [];
    private readonly Random _random;
    private readonly double _duplicateRatio;
    private readonly int _minLength;
    private readonly int _maxLength;

    public int PoolSize => _pool.Count;

    public StringPool(double duplicateRatio = 0.3, int minLength = 5, int maxLength = 50, int? seed = null)
    {
        _duplicateRatio = Math.Clamp(duplicateRatio, 0.0, 1.0);
        _minLength = minLength;
        _maxLength = maxLength;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Gets a string as UTF8 bytes, possibly a duplicate from the pool.
    /// </summary>
    public byte[] GetString()
    {
        // Decide whether to return a duplicate
        if (_pool.Count > 0 && _random.NextDouble() < _duplicateRatio)
        {
            return _pool[_random.Next(_pool.Count)];
        }

        // Generate a new string
        var newString = GenerateRandomString();
        _pool.Add(newString);

        // Limit pool size to prevent unbounded growth
        if (_pool.Count > MaxPoolSize)
        {
            _pool.RemoveAt(_random.Next(_pool.Count));
        }

        return newString;
    }

    private byte[] GenerateRandomString()
    {
        var length = _random.Next(_minLength, _maxLength + 1);
        var bytes = new byte[length];

        // First character is uppercase (A=65, Z=90)
        bytes[0] = (byte)('A' + _random.Next(26));

        // Rest are lowercase letters and spaces
        for (var i = 1; i < length; i++)
        {
            if (_random.NextDouble() < 0.1) // 10% chance of space
            {
                bytes[i] = (byte)' ';
            }
            else
            {
                bytes[i] = (byte)('a' + _random.Next(26));
            }
        }

        return bytes;
    }
}
