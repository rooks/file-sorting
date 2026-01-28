using System.Text;
using FileSorting.Generator;
using Xunit;

namespace FileSorting.Generator.Tests;

public class DictionaryStringPoolTests
{
    [Fact]
    public void CreateDefault_ReturnsWorkingPool()
    {
        var pool = DictionaryStringPool.CreateDefault();

        Assert.True(pool.Count > 0);
        var result = pool.GetString(0);
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
            var pool = DictionaryStringPool.FromFile(tempFile);

            Assert.Equal(3, pool.Count);

            var validWords = new HashSet<string> { "Apple", "Banana", "Cherry" };

            for (var i = 0; i < pool.Count; i++)
            {
                var word = Encoding.UTF8.GetString(pool.GetString(i));
                Assert.Contains(word, validWords);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetString_SameIndexReturnsSameWord()
    {
        var pool = DictionaryStringPool.CreateDefault();

        for (var i = 0; i < 10; i++)
        {
            var word1 = pool.GetString(i);
            var word2 = pool.GetString(i);
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
            var pool = DictionaryStringPool.FromFile(tempFile);

            Assert.Equal(2, pool.Count);

            var word1 = Encoding.UTF8.GetString(pool.GetString(0));
            var word2 = Encoding.UTF8.GetString(pool.GetString(1));

            Assert.Equal("CustomWord1", word1);
            Assert.Equal("CustomWord2", word2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromFile_RepeatedEntriesIncludedInCount()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Apple appears 3 times, Banana appears 1 time
            File.WriteAllLines(tempFile, ["Apple", "Apple", "Apple", "Banana"]);
            var pool = DictionaryStringPool.FromFile(tempFile);

            // All entries are included, so Count is 4
            Assert.Equal(4, pool.Count);

            // First 3 entries should be "Apple"
            for (var i = 0; i < 3; i++)
            {
                var word = Encoding.UTF8.GetString(pool.GetString(i));
                Assert.Equal("Apple", word);
            }

            // Last entry should be "Banana"
            var lastWord = Encoding.UTF8.GetString(pool.GetString(3));
            Assert.Equal("Banana", lastWord);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
