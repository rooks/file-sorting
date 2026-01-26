using System.Threading.Channels;
using FileSorting.Shared;

namespace FileSorting.Sorter;

/// <summary>
/// Main external merge sort orchestrator.
/// </summary>
public sealed class ExternalMergeSorter
{
    private readonly SorterOptions _options;
    private readonly IProgress<SortProgress>? _progress;

    public ExternalMergeSorter(SorterOptions options, IProgress<SortProgress>? progress = null)
    {
        _options = options;
        _progress = progress;
    }

    public async Task SortAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
    {
        using var tempManager = new TempFileManager(_options.TempDirectory);

        try
        {
            // Phase 1: Read and sort chunks
            var chunkFiles = await SortChunksAsync(inputPath, tempManager, cancellationToken);

            if (chunkFiles.Count == 0)
            {
                // Empty file
                await File.WriteAllTextAsync(outputPath, "", cancellationToken);
                return;
            }

            _progress?.Report(new SortProgress(SortPhase.Merging, 0, chunkFiles.Count));

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

            _progress?.Report(new SortProgress(SortPhase.Completed, chunkFiles.Count, chunkFiles.Count));
        }
        finally
        {
            tempManager.Cleanup();
        }
    }

    private async Task<List<string>> SortChunksAsync(
        string inputPath,
        TempFileManager tempManager,
        CancellationToken cancellationToken)
    {
        var chunkFiles = new List<string>();
        var fileInfo = new FileInfo(inputPath);
        var totalSize = fileInfo.Length;

        _progress?.Report(new SortProgress(SortPhase.Chunking, 0, totalSize));

        // Use a channel for producer-consumer pattern
        var channel = Channel.CreateBounded<(Memory<byte> Chunk, string OutputPath)>(
            new BoundedChannelOptions(_options.ParallelDegree)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        // Start consumer tasks
        var consumers = Enumerable.Range(0, _options.ParallelDegree)
            .Select(_ => ProcessChunksAsync(channel.Reader, cancellationToken))
            .ToArray();

        // Producer: read chunks and send to channel
        using var reader = new ChunkReader(inputPath);
        long bytesRead = 0;

        while (!reader.IsCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = await reader.ReadChunkAsync(_options.ChunkSize, cancellationToken);
            if (!chunk.HasValue || chunk.Value.Length == 0)
                break;

            var outputPath = tempManager.CreateChunkFile();
            chunkFiles.Add(outputPath);

            await channel.Writer.WriteAsync((chunk.Value, outputPath), cancellationToken);

            bytesRead += chunk.Value.Length;
            _progress?.Report(new SortProgress(SortPhase.Chunking, bytesRead, totalSize));
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
    public int ChunkSize { get; init; } = CalculateDefaultChunkSize();
    public int ParallelDegree { get; init; } = Environment.ProcessorCount;
    public string? TempDirectory { get; init; }

    private static int CalculateDefaultChunkSize()
    {
        // Use ~60% of available memory divided by number of cores
        var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var memoryPerCore = (long)(availableMemory * Constants.MemoryUsageRatio / Environment.ProcessorCount);

        return (int)Math.Clamp(memoryPerCore, Constants.MinChunkSize, Constants.MaxChunkSize);
    }
}

public enum SortPhase
{
    Chunking,
    Merging,
    Completed
}

public readonly struct SortProgress
{
    public SortPhase Phase { get; }
    public long Current { get; }
    public long Total { get; }

    public SortProgress(SortPhase phase, long current, long total)
    {
        Phase = phase;
        Current = current;
        Total = total;
    }

    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}
