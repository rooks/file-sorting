using System.Buffers;
using FileSorting.Shared.Progress;

namespace FileSorting.Sorter;

/// <summary>
/// Performs k-way merge of sorted files using a priority queue.
/// </summary>
public sealed class KWayMerger(
    ITasksProgress progress,
    int parallelDegree)
{
    private const int FileStreamBufferSize = 16 * 1024 * 1024; // 16MB
    private static readonly byte[] NewLine = [Constants.NewLineCh];

    public async Task MergeAsync(
        string[] inputFiles,
        TempFileManager tempFileManager,
        string outputPath,
        CancellationToken ct = default)
    {
        var mergeWidth = CalculateMergeWidth(inputFiles.Length);

        progress.Start("Merging", inputFiles.Length);

        if (inputFiles.Length <= mergeWidth)
        {
            await MergeSinglePassAsync(inputFiles, outputPath, ct);
            progress.Update(inputFiles.Length);
        }
        else
        {
            var finalTemp = await MergeMultiPassAsync(
                inputFiles,
                mergeWidth,
                tempFileManager,
                ct);

            File.Move(finalTemp, outputPath, overwrite: true);
        }

        progress.Stop();
    }

    private int CalculateMergeWidth(int chunkFileCount)
    {
        if (chunkFileCount <= 1)
            return 1;

        var adaptiveWidth = Math.Clamp(
            parallelDegree * 4,
            Constants.MinMergeWidth,
            Constants.MaxMergeWidth);

        return Math.Clamp(adaptiveWidth, 2, chunkFileCount);
    }

    private async Task MergeSinglePassAsync(
        string[] inputFiles,
        string outputPath,
        CancellationToken ct = default)
    {
        if (inputFiles.Length == 0)
            throw new ArgumentException("No input files to merge", nameof(inputFiles));

        if (inputFiles.Length == 1)
        {
            File.Copy(inputFiles[0], outputPath, overwrite: true);
            return;
        }

        var readers = new SortedFileReader[inputFiles.Length];
        var pq = new PriorityQueue<MergeEntry, MergeEntry>(MergeEntryComparer.Instance);

        try
        {
            // Initialize readers and prime the priority queue
            for (var i = 0; i < inputFiles.Length; i++)
            {
                readers[i] = new SortedFileReader(inputFiles[i], i);
                var entry = await readers[i].ReadNextAsync(ct);
                if (entry.HasValue)
                {
                    pq.Enqueue(entry.Value, entry.Value);
                }
            }

            await using var outStream = new FileStream(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: FileStreamBufferSize,
                FileOptions.SequentialScan);

            var writeBuffer = ArrayPool<byte>.Shared.Rent(Constants.WriteBufferSize);
            try
            {
                var bufferPos = 0;
                long linesWritten = 0;
                const long reportInterval = 100_000;

                while (pq.Count > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    var smallest = pq.Dequeue();

                    // Write the line using binary buffer (no string allocation)
                    var lineLength = smallest.LineBuffer.Length;
                    var requiredSize = lineLength + 1; // +1 for newline

                    // Flush buffer if this line won't fit
                    if (bufferPos + requiredSize > writeBuffer.Length)
                    {
                        if (bufferPos > 0)
                        {
                            outStream.Write(writeBuffer.AsSpan(0, bufferPos));
                            bufferPos = 0;
                        }

                        // If single line is larger than buffer, write directly
                        if (requiredSize > writeBuffer.Length)
                        {
                            outStream.Write(smallest.LineBuffer.Span);
                            outStream.Write(NewLine);
                            linesWritten++;
                            goto readNext;
                        }
                    }

                    // Copy line to buffer
                    smallest.LineBuffer.Span.CopyTo(writeBuffer.AsSpan(bufferPos));
                    bufferPos += lineLength;
                    writeBuffer[bufferPos++] = (byte)'\n';
                    linesWritten++;

                readNext:
                    if (linesWritten % reportInterval == 0)
                    {
                        progress.Update(linesWritten);
                    }

                    // Read next line from the same file
                    var reader = readers[smallest.FileIndex];
                    var next = await reader.ReadNextAsync(ct);
                    if (next.HasValue)
                    {
                        pq.Enqueue(next.Value, next.Value);
                    }
                }

                // Flush remaining data
                if (bufferPos > 0)
                {
                    outStream.Write(writeBuffer.AsSpan(0, bufferPos));
                }

                progress.Update(linesWritten);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(writeBuffer);
            }
        }
        finally
        {
            foreach (var reader in readers)
            {
                reader.Dispose();
            }
        }
    }

    private async Task<string> MergeMultiPassAsync(
        string[] inputFiles,
        int mergeWidth,
        TempFileManager tempManager,
        CancellationToken ct = default)
    {
        var currentFiles = inputFiles;
        var pass = 0;
        var completedMerges = 0L;
        var mergeParallelDegree = Math.Max(1, parallelDegree / 2);

        while (currentFiles.Length > mergeWidth)
        {
            var batches = currentFiles.Chunk(mergeWidth).ToArray();
            var nextFiles = new string[batches.Length];

            await Parallel.ForEachAsync(
                Enumerable.Range(0, batches.Length),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = mergeParallelDegree,
                    CancellationToken = ct
                },
                async (index, token) =>
                {
                    var outputFile = tempManager.CreateMergeFile(pass, index);
                    nextFiles[index] = outputFile;
                    await MergeSinglePassAsync(
                        batches[index],
                        outputFile,
                        token);

                    var mergedCount = Interlocked.Increment(ref completedMerges);
                    progress.Update(mergedCount);
                });

            currentFiles = nextFiles;
            pass++;
        }

        // Final merge - return the path but don't write to final destination yet
        if (currentFiles.Length == 1)
        {
            return currentFiles[0];
        }

        var finalTemp = tempManager.CreateMergeFile(pass, 0);
        await MergeSinglePassAsync(currentFiles, finalTemp, ct);
        var finalMergeCount = Interlocked.Increment(ref completedMerges);
        progress.Update(finalMergeCount);
        return finalTemp;
    }
}
