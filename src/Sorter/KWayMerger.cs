namespace FileSorting.Sorter;

/// <summary>
/// Performs k-way merge of sorted files using a priority queue.
/// </summary>
public sealed class KWayMerger
{
    private static readonly System.Text.UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private readonly IProgress<long>? _progress;

    public KWayMerger(IProgress<long>? progress = null)
    {
        _progress = progress;
    }

    /// <summary>
    /// Merges multiple sorted files into a single output file.
    /// </summary>
    public async Task MergeAsync(
        IReadOnlyList<string> inputFiles,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (inputFiles.Count == 0)
            throw new ArgumentException("No input files to merge", nameof(inputFiles));

        if (inputFiles.Count == 1)
        {
            // Just copy the single file
            File.Copy(inputFiles[0], outputPath, overwrite: true);
            return;
        }

        var readers = new SortedFileReader[inputFiles.Count];
        var pq = new PriorityQueue<MergeEntry, MergeEntry>(MergeEntryComparer.Instance);

        try
        {
            // Initialize readers and prime the priority queue
            for (int i = 0; i < inputFiles.Count; i++)
            {
                readers[i] = new SortedFileReader(inputFiles[i], i);
                var entry = await readers[i].ReadNextAsync(cancellationToken);
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
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using var writer = new StreamWriter(outStream, Utf8NoBom, bufferSize: 64 * 1024);

            long linesWritten = 0;
            const long reportInterval = 100_000;

            while (pq.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var smallest = pq.Dequeue();

                // Write the line
                await writer.WriteLineAsync(smallest.Line.ToString());
                linesWritten++;

                if (linesWritten % reportInterval == 0)
                {
                    _progress?.Report(linesWritten);
                }

                // Read next line from the same file
                var reader = readers[smallest.FileIndex];
                var next = await reader.ReadNextAsync(cancellationToken);
                if (next.HasValue)
                {
                    pq.Enqueue(next.Value, next.Value);
                }
            }

            _progress?.Report(linesWritten);
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
        int pass = 0;

        while (currentFiles.Count > mergeWidth)
        {
            var nextFiles = new List<string>();
            var batches = currentFiles.Chunk(mergeWidth).ToList();

            for (int i = 0; i < batches.Count; i++)
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
