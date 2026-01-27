using System.Buffers;
using System.IO.Pipelines;

namespace FileSorting.Generator;

public sealed class FileGenerator(
    LineGenerator lineGenerator,
    IProgress<long>? progress = null)
{
    // Max bytes a single line can occupy (number + ". " + string + newline)
    // Conservative estimate: 10 digits + 2 + ~500 char string + 1
    private const int MaxLineSize = 1024;

    public async Task GenerateAsync(
        string outputPath,
        long targetSize,
        CancellationToken cancellationToken = default)
    {
        // Scale buffer sizes based on target file size
        var (fileStreamBuffer, pipeBuffer, flushInterval) = GetBufferSizes(targetSize);

        await using var fileStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: fileStreamBuffer,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var writer = PipeWriter.Create(fileStream, new StreamPipeWriterOptions(leaveOpen: false, minimumBufferSize: pipeBuffer));

        var bytesWritten = 0L;
        var lastReported = 0L;

        try
        {
            while (bytesWritten < targetSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Get a larger buffer and write multiple lines
                var buffer = writer.GetMemory(pipeBuffer);
                var offset = 0;

                // Fill the buffer with multiple lines
                while (offset + MaxLineSize < buffer.Length && bytesWritten + offset < targetSize)
                {
                    var lineBytes = lineGenerator.WriteLine(buffer.Span[offset..]);
                    offset += lineBytes;
                }

                writer.Advance(offset);
                bytesWritten += offset;

                // Flush periodically to avoid unbounded memory growth
                if (bytesWritten - lastReported >= flushInterval)
                {
                    var flushResult = await writer.FlushAsync(cancellationToken);
                    if (flushResult.IsCompleted)
                        break;

                    progress?.Report(bytesWritten);
                    lastReported = bytesWritten;
                }
            }

            await writer.CompleteAsync();
            progress?.Report(bytesWritten);
        }
        catch (Exception ex)
        {
            await writer.CompleteAsync(ex);
            throw;
        }
    }

    private static (int fileStreamBuffer, int pipeBuffer, long flushInterval) GetBufferSizes(long targetSize)
    {
        return targetSize switch
        {
            < 1 * 1024 * 1024 => (           // < 1MB
                fileStreamBuffer: 16 * 1024,             // 16KB
                pipeBuffer: 8 * 1024,                    // 8KB
                flushInterval: targetSize / 4),          // 4 reports total

            < 100 * 1024 * 1024 => (         // < 100MB
                fileStreamBuffer: 256 * 1024,            // 256KB
                pipeBuffer: 64 * 1024,                   // 64KB
                flushInterval: 10 * 1024 * 1024),        // 10MB

            < 1024L * 1024 * 1024 => (       // < 1GB
                fileStreamBuffer: 1024 * 1024,           // 1MB
                pipeBuffer: 256 * 1024,                  // 256KB
                flushInterval: 50 * 1024 * 1024),        // 50MB

            _ => (                           // >= 1GB
                fileStreamBuffer: 4 * 1024 * 1024,       // 4MB
                pipeBuffer: 512 * 1024,                  // 512KB
                flushInterval: 100 * 1024 * 1024)        // 100MB
        };
    }
}
