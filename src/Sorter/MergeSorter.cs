using System.Buffers;
using FileSorting.Shared.Progress;

namespace FileSorting.Sorter;

public sealed class MergeSorter(
    int chunkSize,
    int parallelDegree,
    ITasksProgress progress,
    string? tempDirectory = null)
{
    private readonly record struct FileRange(long Start, long End);

    public async Task SortAsync(
        string inputPath,
        string outputPath,
        CancellationToken ct = default)
    {
        using var tempManager = new TempFileManager(tempDirectory);

        var chunkFiles = await SortChunksAsync(inputPath, tempManager, ct);
        if (chunkFiles.Length == 0)
        {
            await File.WriteAllTextAsync(outputPath, string.Empty, ct);
            return;
        }

        var merger = new KWayMerger(progress, parallelDegree);

        await merger.MergeAsync(
            chunkFiles,
            tempManager,
            outputPath,
            ct);
    }

    private async Task<string[]> SortChunksAsync(
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

        return chunkFiles;
    }

    private List<FileRange> CalculateRanges(FileInfo input)
    {
        var fileLength = input.Length;
        if (fileLength == 0)
            return [];

        var rangeCount = (int)Math.Ceiling((double)fileLength / chunkSize);
        if (rangeCount <= 1)
            return [new FileRange(0, fileLength)];

        progress.Start("Calculate ranges", rangeCount);

        var boundaries = new long[rangeCount + 1];
        boundaries[0] = 0;
        boundaries[rangeCount] = fileLength;

        using var probeStream = new FileStream(
            input.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: Constants.BoundaryProbeBufferSize,
            FileOptions.RandomAccess);

        var probeBuffer = new byte[Constants.BoundaryProbeBufferSize];

        for (var i = 1; i < rangeCount; i++)
        {
            var candidate = (long)i * chunkSize;
            var boundary = FindNextBoundary(probeStream, probeBuffer, candidate, fileLength);
            boundaries[i] = Math.Max(boundaries[i - 1], boundary);

            // No more newline found after this candidate.
            if (boundaries[i] == fileLength)
            {
                for (var j = i + 1; j < rangeCount; j++)
                {
                    boundaries[j] = fileLength;
                }

                break;
            }

            progress.Update(i);
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

        progress.Stop();

        return ranges;
    }

    private static long FindNextBoundary(
        FileStream probeStream,
        byte[] probeBuffer,
        long candidate,
        long fileLength)
    {
        if (candidate >= fileLength)
            return fileLength;

        var position = candidate;
        while (position < fileLength)
        {
            probeStream.Seek(position, SeekOrigin.Begin);
            var bytesToRead = (int)Math.Min(probeBuffer.Length, fileLength - position);
            var bytesRead = probeStream.Read(probeBuffer, 0, bytesToRead);
            if (bytesRead <= 0)
                return fileLength;

            var newlineIndex = probeBuffer.AsSpan(0, bytesRead).IndexOf((byte)'\n');
            if (newlineIndex >= 0)
                return position + newlineIndex + 1;

            position += bytesRead;
        }

        return fileLength;
    }

    private static Task ProcessRangeAsync(
        string inputPath,
        FileRange range,
        string outputPath,
        CancellationToken ct)
    {
        var length = (int)(range.End - range.Start);
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            using (var stream = new FileStream(
                inputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: Constants.ChunkReadBufferSize,
                FileOptions.SequentialScan))
            {
                stream.Seek(range.Start, SeekOrigin.Begin);
                stream.ReadExactly(buffer.AsSpan(0, length));
            }

            var chunkMemory = buffer.AsMemory(0, length);
            var sortedLines = ChunkSorter.SortChunk(chunkMemory);
            ChunkSorter.WriteChunk(sortedLines, outputPath, ct);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return Task.CompletedTask;
    }
}
