using System.Buffers;
using System.Threading.Channels;

namespace FileSorting.Generator;

/// <summary>
/// High-performance parallel file generator using multiple worker threads.
/// Each worker generates chunks independently, which are then written sequentially.
/// </summary>
public sealed class ParallelFileGenerator
{
    private const int ChunkSize = 1 * 1024 * 1024; // 1MB chunks
    private const int FileStreamBuffer = 16 * 1024 * 1024; // 16MB
    private const int MaxLineSize = 1024;

    private readonly DictionaryStringPool _stringPool;
    private readonly int _workerCount;
    private readonly IProgress<long>? _progress;

    public ParallelFileGenerator(
        DictionaryStringPool stringPool,
        int? workerCount = null,
        IProgress<long>? progress = null)
    {
        _stringPool = stringPool;
        _workerCount = workerCount ?? Environment.ProcessorCount;
        _progress = progress;
    }

    public async Task GenerateAsync(
        string outputPath,
        long targetSize,
        int? seed = null,
        CancellationToken ct = default)
    {
        // Bounded channel to limit memory usage - workers wait if writer is slow
        var channel = Channel.CreateBounded<ChunkData>(new BoundedChannelOptions(_workerCount * 2)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        // Distribute work across workers
        var bytesPerWorker = targetSize / _workerCount;
        var remainder = targetSize % _workerCount;

        // Start workers
        var workers = new Task[_workerCount];
        for (var i = 0; i < _workerCount; i++)
        {
            // Last worker handles the remainder
            var workerBytes = bytesPerWorker + (i == _workerCount - 1 ? remainder : 0);
            // Each worker gets a unique seed derived from the base seed
            var workerSeed = seed + i;
            workers[i] = GenerateChunksAsync(channel.Writer, workerBytes, workerSeed, ct);
        }

        // Start single writer
        var writer = WriteChunksAsync(outputPath, channel.Reader, targetSize, ct);

        // Wait for all workers to complete, then signal channel completion
        await Task.WhenAll(workers);
        channel.Writer.Complete();

        // Wait for writer to finish
        await writer;
    }

    private async Task GenerateChunksAsync(
        ChannelWriter<ChunkData> writer,
        long targetBytes,
        int? seed,
        CancellationToken ct)
    {
        var lineGenerator = new LineGenerator(_stringPool, seed: seed);
        var bytesGenerated = 0L;

        while (bytesGenerated < targetBytes)
        {
            ct.ThrowIfCancellationRequested();

            // Rent a buffer from the pool
            var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
            var offset = 0;

            // Fill the buffer with lines
            var remainingBytes = targetBytes - bytesGenerated;
            var chunkTarget = (int)Math.Min(ChunkSize - MaxLineSize, remainingBytes);

            while (offset < chunkTarget)
            {
                var lineBytes = lineGenerator.WriteLine(buffer.AsSpan(offset));
                offset += lineBytes;
            }

            bytesGenerated += offset;

            // Send chunk to writer
            await writer.WriteAsync(new ChunkData(buffer, offset), ct);
        }
    }

    private async Task WriteChunksAsync(
        string outputPath,
        ChannelReader<ChunkData> reader,
        long targetSize,
        CancellationToken ct)
    {
        await using var fileStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: FileStreamBuffer,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var bytesWritten = 0L;
        var lastReported = 0L;
        var reportInterval = GetReportInterval(targetSize);

        await foreach (var chunk in reader.ReadAllAsync(ct))
        {
            // Write chunk to file
            await fileStream.WriteAsync(chunk.Buffer.AsMemory(0, chunk.Length), ct);
            bytesWritten += chunk.Length;

            // Return buffer to pool
            ArrayPool<byte>.Shared.Return(chunk.Buffer);

            // Report progress periodically
            if (bytesWritten - lastReported >= reportInterval)
            {
                _progress?.Report(bytesWritten);
                lastReported = bytesWritten;
            }
        }

        _progress?.Report(bytesWritten);
    }

    private static long GetReportInterval(long targetSize)
    {
        return targetSize switch
        {
            < 1 * 1024 * 1024 => targetSize / 4,
            < 100 * 1024 * 1024 => 10 * 1024 * 1024,
            < 1024L * 1024 * 1024 => 50 * 1024 * 1024,
            _ => 100 * 1024 * 1024
        };
    }

    private readonly record struct ChunkData(byte[] Buffer, int Length);
}
