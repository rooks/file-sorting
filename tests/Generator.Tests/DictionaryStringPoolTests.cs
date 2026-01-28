using System.Text;
using FileSorting.Generator;
using Xunit;

namespace FileSorting.Generator.Tests;

public class DictionaryStringPoolTests
{
    [Fact]
    public void CreateDefault_ReturnsWorkingPool()
    {
        var pool = DictionaryStringPool.CreateDefault(seed: 42);

        var result = pool.GetString();

        Assert.NotNull(result);
        Assert.True(result.Length > 0);
    }

    [Fact]
    public void GetString_ReturnsWordsFromDictionary()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, ["Apple", "Banana", "Cherry"]);
            var pool = DictionaryStringPool.FromFile(tempFile, seed: 42);

            var validWords = new HashSet<string> { "Apple", "Banana", "Cherry" };

            for (var i = 0; i < 100; i++)
            {
                var word = Encoding.UTF8.GetString(pool.GetString());
                Assert.Contains(word, validWords);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetString_DeterministicWithSeed()
    {
        var pool1 = DictionaryStringPool.CreateDefault(seed: 123);
        var pool2 = DictionaryStringPool.CreateDefault(seed: 123);

        for (var i = 0; i < 100; i++)
        {
            var word1 = pool1.GetString();
            var word2 = pool2.GetString();
            Assert.True(word1.AsSpan().SequenceEqual(word2));
        }
    }

    [Fact]
    public void FromFile_ThrowsOnEmptyFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "");

            Assert.Throws<InvalidOperationException>(() => DictionaryStringPool.FromFile(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromFile_LoadsCustomDictionary()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, ["CustomWord1", "CustomWord2"]);
            var pool = DictionaryStringPool.FromFile(tempFile, seed: 42);

            var validWords = new HashSet<string> { "CustomWord1", "CustomWord2" };
            var foundWords = new HashSet<string>();

            for (var i = 0; i < 100; i++)
            {
                var word = Encoding.UTF8.GetString(pool.GetString());
                Assert.Contains(word, validWords);
                foundWords.Add(word);
            }

            // Should eventually hit both words
            Assert.Equal(2, foundWords.Count);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetString_FrequencyControlledByRepeatedEntries()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Apple appears 3 times, Banana appears 1 time
            File.WriteAllLines(tempFile, ["Apple", "Apple", "Apple", "Banana"]);
            var pool = DictionaryStringPool.FromFile(tempFile, seed: 42);

            var appleCount = 0;
            var bananaCount = 0;
            const int iterations = 10000;

            for (var i = 0; i < iterations; i++)
            {
                var word = Encoding.UTF8.GetString(pool.GetString());
                if (word == "Apple") appleCount++;
                else if (word == "Banana") bananaCount++;
            }

            // Apple should appear roughly 3x more often than Banana
            var ratio = (double)appleCount / bananaCount;
            Assert.InRange(ratio, 2.0, 4.0); // Allow some variance
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
