namespace FileSorting.Shared;

public static class SizeParser
{
    private static readonly string[] Suffixes = ["B", "KB", "MB", "GB", "TB"];
    private static readonly Dictionary<string, long> SuffixToMult = new(StringComparer.OrdinalIgnoreCase)
    {
        ["B" ] = 1L,
        ["KB"] = 1024L,
        ["K" ] = 1024L,
        ["MB"] = 1024L * 1024,
        ["M" ] = 1024L * 1024,
        ["GB"] = 1024L * 1024 * 1024,
        ["G" ] = 1024L * 1024 * 1024,
        ["TB"] = 1024L * 1024 * 1024 * 1024,
        ["T" ] = 1024L * 1024 * 1024 * 1024,
    };

    public static long Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Size cannot be empty", nameof(input));

        var span = input.AsSpan().Trim();

        var suffixStart = span.Length;
        while (suffixStart > 0 && !char.IsDigit(span[suffixStart - 1]))
        {
            suffixStart--;
        }

        if (suffixStart == 0)
            throw new FormatException($"Invalid size format: '{input}'");

        var numberPart = span[..suffixStart];
        var suffixPart = span[suffixStart..];

        if (!double.TryParse(numberPart, out var number))
            throw new FormatException($"Invalid number in size: '{input}'");

        var multiplier = 1L;
        if (suffixPart.Length > 0)
        {
            var suffix = suffixPart.ToString();
            if (!SuffixToMult.TryGetValue(suffix, out multiplier))
                throw new FormatException($"Unknown size suffix: '{suffix}'");
        }

        return (long)(number * multiplier);
    }

    public static bool TryParse(string input, out long bytes)
    {
        try
        {
            bytes = Parse(input);
            return true;
        }
        catch
        {
            bytes = 0;
            return false;
        }
    }

    public static string Format(long bytes)
    {
        var index = 0;
        double value = bytes;

        while (value >= 1024 && index < Suffixes.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return value == (long)value
            ? $"{(long)value}{Suffixes[index]}"
            : $"{value:F2}{Suffixes[index]}";
    }
}
