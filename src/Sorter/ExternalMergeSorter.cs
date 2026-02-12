using System.Buffers;
using FileSorting.Shared.Progress;

namespace FileSorting.Sorter;

internal readonly record struct FileRange(long Start, long End);

/// <summary>
/// Main external merge sort orchestrator.
/// </summary>
public sealed class ExternalMergeSorter(
    int chunkSize,
    int parallelDegree,
    ITasksProgress progress,
    string? tempDirectory = null)
{
    private const int ProbeBufferSize = 8 * 1024; // 8KB for boundary probing

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

        progress.Stop();
    }

    private async Task<List<string>> SortChunksAsync(
        string inputPath,
        TempFileManager tempManager,
        CancellationToken ct)
    {
        var input = new FileInfo(inputPath);
        var ranges = CalculateRanges(input);
        if (ranges.Count == 0)
            return [];

        progress.Start("Chunking", input.Length);

        var chunkFiles = new string[ranges.Count];
        var bytesProcessed = 0L;

        await Parallel.ForEachAsync(
            Enumerable.Range(0, ranges.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = parallelDegree,
                CancellationToken = ct
            },
            async (index, token) =>
            {
                var range = ranges[index];
                var outputPath = tempManager.CreateChunkFile();
                chunkFiles[index] = outputPath;
                await ProcessRangeAsync(input.FullName, range, outputPath, token);

                var rangeSize = range.End - range.Start;
                var current = Interlocked.Add(ref bytesProcessed, rangeSize);
                progress.Update(current);
            });

        progress.Stop();

        return [..chunkFiles];
    }

    private List<FileRange> CalculateRanges(FileInfo input)
    {
        var fileLength = input.Length;
        if (fileLength == 0)
            return [];

        var rangeCount = (int)Math.Ceiling((double)fileLength / chunkSize);
        if (rangeCount <= 1)
            return [new FileRange(0, fileLength)];

        var boundaries = new long[rangeCount + 1];
        boundaries[0] = 0;
        boundaries[rangeCount] = fileLength;

        using var probeStream = new FileStream(
            input.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: ProbeBufferSize,
            FileOptions.RandomAccess);

        var probeBuffer = new byte[ProbeBufferSize];

        for (var i = 1; i < rangeCount; i++)
        {
            var candidate = (long)i * chunkSize;

            probeStream.Seek(candidate, SeekOrigin.Begin);
            var bytesRead = probeStream.Read(probeBuffer, 0, (int)Math.Min(ProbeBufferSize, fileLength - candidate));

            var newlineIndex = probeBuffer.AsSpan(0, bytesRead).IndexOf((byte)'\n');
            if (newlineIndex >= 0)
            {
                boundaries[i] = candidate + newlineIndex + 1;
            }
            else
            {
                // No newline found in probe — skip this boundary,
                // previous range grows
                boundaries[i] = boundaries[i - 1];
            }
        }

        // Build ranges, skipping any zero-length ranges from collapsed boundaries
        var ranges = new List<FileRange>(rangeCount);
        for (var i = 0; i < rangeCount; i++)
        {
            var start = boundaries[i];
            var end = boundaries[i + 1];
            if (end > start)
                ranges.Add(new FileRange(start, end));
        }

        return ranges;
    }

    private static async Task ProcessRangeAsync(
        string inputPath,
        FileRange range,
        string outputPath,
        CancellationToken ct)
    {
        var length = (int)(range.End - range.Start);
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            await using (var stream = new FileStream(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.RandomAccess))
            {
                stream.Seek(range.Start, SeekOrigin.Begin);
                await stream.ReadExactlyAsync(buffer.AsMemory(0, length), ct);
            }

            var chunkMemory = buffer.AsMemory(0, length);
            var sortedLines = ChunkSorter.SortChunk(chunkMemory);
            await ChunkSorter.WriteChunkAsync(sortedLines, outputPath, ct);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
