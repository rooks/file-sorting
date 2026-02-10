using System.Buffers;
using System.Threading.Channels;
using FileSorting.Shared.Progress;

namespace FileSorting.Sorter;

/// <summary>
/// Main external merge sort orchestrator.
/// </summary>
public sealed class ExternalMergeSorter(
    int chunkSize,
    int parallelDegree,
    ITasksProgress progress,
    string? tempDirectory = null)
{
    public async Task SortAsync(
        string inputPath,
        string outputPath,
        CancellationToken ct = default)
    {
        using var tempManager = new TempFileManager(tempDirectory);

        var chunkFiles = await SortChunksAsync(inputPath, tempManager, ct);
        if (chunkFiles.Count == 0)
        {
            await File.WriteAllTextAsync(outputPath, string.Empty, ct);
            return;
        }

        progress.Start("Merging", chunkFiles.Count);

        var merger = new KWayMerger(progress);

        if (chunkFiles.Count <= Constants.MergeWidth)
        {
            // Single-pass merge directly to output
            await merger.MergeAsync(chunkFiles, outputPath, ct);
        }
        else
        {
            var finalTemp = await merger.MultiPassMergeAsync(
                chunkFiles,
                Constants.MergeWidth,
                tempManager,
                ct);

            File.Move(finalTemp, outputPath, overwrite: true);
        }

        // in between??

        progress.Stop();
    }

    private async Task<List<string>> SortChunksAsync(
        string inputPath,
        TempFileManager tempManager,
        CancellationToken ct)
    {
        var chunkFiles = new List<string>();
        var fileInfo = new FileInfo(inputPath);
        var totalSize = fileInfo.Length;

        progress.Start("Chunking", totalSize);

        // Use a channel for producer-consumer pattern
        // Chunk is (Buffer, Length) where Buffer is rented from ArrayPool
        var channel = Channel.CreateBounded<(byte[] Buffer, int Length, string OutputPath)>(
            new BoundedChannelOptions(parallelDegree)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        // Start consumer tasks
        var consumers = Enumerable.Range(0, parallelDegree)
            .Select(_ => ProcessChunksAsync(channel.Reader, ct))
            .ToArray();

        // Producer: read chunks and send to channel
        using var reader = new ChunkReader(inputPath);
        var bytesRead = 0L;

        while (!reader.IsCompleted)
        {
            ct.ThrowIfCancellationRequested();

            var chunk = await reader.ReadChunkAsync(chunkSize, ct);
            if (!chunk.HasValue || chunk.Value.Length == 0)
                break;

            var (buffer, length) = chunk.Value;
            var outputPath = tempManager.CreateChunkFile();
            chunkFiles.Add(outputPath);

            await channel.Writer.WriteAsync((buffer, length, outputPath), ct);

            bytesRead += length;
            progress.Update(bytesRead);
        }

        channel.Writer.Complete();

        // Wait for all consumers to finish
        await Task.WhenAll(consumers);

        return chunkFiles;
    }

    private static async Task ProcessChunksAsync(
        ChannelReader<(byte[] Buffer, int Length, string OutputPath)> reader,
        CancellationToken ct)
    {
        await foreach (var (buffer, length, outputPath) in reader.ReadAllAsync(ct))
        {
            try
            {
                // Create a Memory slice of just the valid data
                var chunkMemory = buffer.AsMemory(0, length);
                var sortedLines = ChunkSorter.SortChunk(chunkMemory);
                await ChunkSorter.WriteChunkAsync(sortedLines, outputPath, ct);
            }
            finally
            {
                // Return the rented buffer to the pool
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
