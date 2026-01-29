using System.Threading.Channels;

namespace FileSorting.Sorter;

/// <summary>
/// Main external merge sort orchestrator.
/// </summary>
public sealed class ExternalMergeSorter(
    SorterOptions options,
    IProgress<SortProgress>? progress = null)
{
    public async Task SortAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
    {
        using var tempManager = new TempFileManager(options.TempDirectory);

        // Phase 1: Read and sort chunks
        var chunkFiles = await SortChunksAsync(inputPath, tempManager, cancellationToken);
        if (chunkFiles.Count == 0) // empty
        {
            await File.WriteAllTextAsync(outputPath, "", cancellationToken);
            return;
        }

        progress?.Report(new SortProgress(SortPhase.Merging, 0, chunkFiles.Count));

        // Phase 2: Merge chunks
        var merger = new KWayMerger();

        if (chunkFiles.Count <= Constants.MergeWidth)
        {
            // Single-pass merge directly to output
            await merger.MergeAsync(chunkFiles, outputPath, cancellationToken);
        }
        else
        {
            // Multi-pass merge
            var finalTemp = await merger.MultiPassMergeAsync(
                chunkFiles,
                Constants.MergeWidth,
                tempManager,
                cancellationToken);

            // Move final result to output
            File.Move(finalTemp, outputPath, overwrite: true);
        }

        progress?.Report(new SortProgress(SortPhase.Completed, chunkFiles.Count, chunkFiles.Count));
    }

    private async Task<List<string>> SortChunksAsync(
        string inputPath,
        TempFileManager tempManager,
        CancellationToken cancellationToken)
    {
        var chunkFiles = new List<string>();
        var fileInfo = new FileInfo(inputPath);
        var totalSize = fileInfo.Length;

        progress?.Report(new SortProgress(SortPhase.Chunking, 0, totalSize));

        // Use a channel for producer-consumer pattern
        var channel = Channel.CreateBounded<(Memory<byte> Chunk, string OutputPath)>(
            new BoundedChannelOptions(options.ParallelDegree)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        // Start consumer tasks
        var consumers = Enumerable.Range(0, options.ParallelDegree)
            .Select(_ => ProcessChunksAsync(channel.Reader, cancellationToken))
            .ToArray();

        // Producer: read chunks and send to channel
        using var reader = new ChunkReader(inputPath);
        var bytesRead = 0L;

        while (!reader.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = await reader.ReadChunkAsync(options.ChunkSize, cancellationToken);
            if (!chunk.HasValue || chunk.Value.Length == 0)
                break;

            var outputPath = tempManager.CreateChunkFile();
            chunkFiles.Add(outputPath);

            await channel.Writer.WriteAsync((chunk.Value, outputPath), cancellationToken);

            bytesRead += chunk.Value.Length;
            progress?.Report(new SortProgress(SortPhase.Chunking, bytesRead, totalSize));
        }

        channel.Writer.Complete();

        // Wait for all consumers to finish
        await Task.WhenAll(consumers);

        return chunkFiles;
    }

    private static async Task ProcessChunksAsync(
        ChannelReader<(Memory<byte> Chunk, string OutputPath)> reader,
        CancellationToken cancellationToken)
    {
        await foreach (var (chunk, outputPath) in reader.ReadAllAsync(cancellationToken))
        {
            var sortedLines = ChunkSorter.SortChunk(chunk);
            await ChunkSorter.WriteChunkAsync(sortedLines, outputPath, cancellationToken);
        }
    }
}

public sealed class SorterOptions
{
    public int ChunkSize { get; init; }
    public int ParallelDegree { get; init; }
    public string? TempDirectory { get; init; }
}

public enum SortPhase
{
    Chunking,
    Merging,
    Completed
}

public readonly struct SortProgress(SortPhase phase, long current, long total)
{
    public SortPhase Phase { get; } = phase;
    public long Current { get; } = current;
    public long Total { get; } = total;

    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}
