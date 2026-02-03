using System.Buffers;
using System.Threading.Channels;

namespace FileSorting.Generator;

public sealed class ParallelFileGenerator(
    DictionaryStringPool stringPool,
    IProgress<long>? progress = null)
{
    private readonly record struct ChunkData(byte[] Buffer, int Length);

    private const int ChunkSize = 1 * 1024 * 1024; // 1MB chunks
    private const int FileStreamBuffer = 16 * 1024 * 1024; // 16MB
    private const int MaxLineSize = 1024;

    private readonly int _workerCount = Environment.ProcessorCount;

    public async Task GenerateAsync(
        string outputPath,
        long targetSize,
        int? seed = null,
        CancellationToken ct = default)
    {
        var opts = new BoundedChannelOptions(_workerCount * 2)
        {
            SingleWriter = false,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        };
        var channel = Channel.CreateBounded<ChunkData>(opts);

        var bytesPerWorker = targetSize / _workerCount;
        var remainder = targetSize % _workerCount;

        var workers = new Task[_workerCount];
        for (var i = 0; i < _workerCount; i++)
        {
            // last worker handles the remainder
            var workerBytes = bytesPerWorker + (i == _workerCount - 1 ? remainder : 0);
            var workerSeed = seed + i;
            workers[i] = GenerateChunksAsync(channel.Writer, workerBytes, workerSeed, ct);
        }

        var writer = WriteChunksAsync(outputPath, channel.Reader, targetSize, ct);

        await Task.WhenAll(workers);
        channel.Writer.Complete();

        await writer;
    }

    private async Task GenerateChunksAsync(
        ChannelWriter<ChunkData> writer,
        long targetBytes,
        int? seed,
        CancellationToken ct)
    {
        var lineGenerator = new LineGenerator(stringPool, seed: seed);
        var bytesGenerated = 0L;

        while (bytesGenerated < targetBytes)
        {
            ct.ThrowIfCancellationRequested();

            var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
            var offset = 0;
            var remainingBytes = targetBytes - bytesGenerated;
            var chunkTarget = (int)Math.Min(ChunkSize - MaxLineSize, remainingBytes);

            while (offset < chunkTarget)
            {
                var lineBytes = lineGenerator.WriteLine(buffer.AsSpan(offset));
                offset += lineBytes;
            }

            bytesGenerated += offset;

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
            await fileStream.WriteAsync(chunk.Buffer.AsMemory(0, chunk.Length), ct);
            bytesWritten += chunk.Length;

            ArrayPool<byte>.Shared.Return(chunk.Buffer);

            if (bytesWritten - lastReported >= reportInterval)
            {
                progress?.Report(bytesWritten);
                lastReported = bytesWritten;
            }
        }

        progress?.Report(bytesWritten);
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
}
