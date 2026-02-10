using System.Buffers;
using FileSorting.Shared.Progress;

namespace FileSorting.Sorter;

/// <summary>
/// Performs k-way merge of sorted files using a priority queue.
/// </summary>
public sealed class KWayMerger(
    ITasksProgress progress)
{
    private const int WriteBufferSize = 64 * 1024; // 64KB write buffer
    private const int FileStreamBufferSize = 16 * 1024 * 1024; // 16MB FileStream buffer
    private static readonly byte[] NewLine = [(byte)'\n'];

    /// <summary>
    /// Merges multiple sorted files into a single output file.
    /// </summary>
    public async Task MergeAsync(
        List<string> inputFiles,
        string outputPath,
        CancellationToken ct = default)
    {
        if (inputFiles.Count == 0)
            throw new ArgumentException("No input files to merge", nameof(inputFiles));

        if (inputFiles.Count == 1)
        {
            File.Copy(inputFiles[0], outputPath, overwrite: true);
            return;
        }

        var readers = new SortedFileReader[inputFiles.Count];
        var pq = new PriorityQueue<MergeEntry, MergeEntry>(MergeEntryComparer.Instance);

        try
        {
            // Initialize readers and prime the priority queue
            for (var i = 0; i < inputFiles.Count; i++)
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
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var writeBuffer = ArrayPool<byte>.Shared.Rent(WriteBufferSize);
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
                            await outStream.WriteAsync(writeBuffer.AsMemory(0, bufferPos), ct);
                            bufferPos = 0;
                        }

                        // If single line is larger than buffer, write directly
                        if (requiredSize > writeBuffer.Length)
                        {
                            await outStream.WriteAsync(smallest.LineBuffer, ct);
                            await outStream.WriteAsync(NewLine, ct);
                            linesWritten++;
                            goto readNext;
                        }
                    }

                    // Copy line to buffer (use Span inside synchronous block)
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
                    await outStream.WriteAsync(writeBuffer.AsMemory(0, bufferPos), ct);
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
                reader?.Dispose();
            }
        }
    }

    /// <summary>
    /// Performs multi-pass merge when there are more files than the merge width.
    /// </summary>
    public async Task<string> MultiPassMergeAsync(
        IReadOnlyList<string> inputFiles,
        int mergeWidth,
        TempFileManager tempManager,
        CancellationToken cancellationToken = default)
    {
        var currentFiles = inputFiles.ToList();
        var pass = 0;

        while (currentFiles.Count > mergeWidth)
        {
            var nextFiles = new List<string>();
            var batches = currentFiles.Chunk(mergeWidth).ToList();

            for (var i = 0; i < batches.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = batches[i];
                var outputFile = tempManager.CreateMergeFile(pass, i);

                await MergeAsync(batch.ToList(), outputFile, cancellationToken);
                nextFiles.Add(outputFile);
            }

            currentFiles = nextFiles;
            pass++;
        }

        // Final merge - return the path but don't write to final destination yet
        if (currentFiles.Count == 1)
        {
            return currentFiles[0];
        }

        var finalTemp = tempManager.CreateMergeFile(pass, 0);
        await MergeAsync(currentFiles, finalTemp, cancellationToken);
        return finalTemp;
    }
}
