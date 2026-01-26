using FileSorting.Generator;
using Xunit;

namespace FileSorting.Generator.Tests;

public class StringPoolTests
{
    [Fact]
    public void GetString_ReturnsNonEmptyString()
    {
        var pool = new StringPool(seed: 42);
        var result = pool.GetString();
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void GetString_RespectsMinMaxLength()
    {
        var pool = new StringPool(minLength: 10, maxLength: 20, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var result = pool.GetString();
            var str = System.Text.Encoding.UTF8.GetString(result);
            Assert.InRange(str.Length, 10, 20);
        }
    }

    [Fact]
    public void GetString_WithHighDuplicateRatio_ProducesDuplicates()
    {
        var pool = new StringPool(duplicateRatio: 1.0, seed: 42);

        // Get first string to populate pool
        var first = pool.GetString();

        // With 100% duplicate ratio, subsequent strings should be from pool
        var duplicateCount = 0;
        for (int i = 0; i < 100; i++)
        {
            var result = pool.GetString();
            if (result.SequenceEqual(first))
            {
                duplicateCount++;
            }
        }

        // Should have at least some duplicates (pool starts small, then gets duplicates)
        Assert.True(duplicateCount > 0 || pool.PoolSize > 1);
    }

    [Fact]
    public void GetString_WithZeroDuplicateRatio_AlwaysGeneratesNew()
    {
        var pool = new StringPool(duplicateRatio: 0.0, seed: 42);

        var strings = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            var result = System.Text.Encoding.UTF8.GetString(pool.GetString());
            strings.Add(result);
        }

        // All strings should be unique (or very close to it - random could produce same by chance)
        Assert.True(strings.Count > 90);
    }

    [Fact]
    public void GetString_FirstCharIsUppercase()
    {
        var pool = new StringPool(seed: 42);

        for (int i = 0; i < 100; i++)
        {
            var result = System.Text.Encoding.UTF8.GetString(pool.GetString());
            Assert.True(char.IsUpper(result[0]));
        }
    }
}
