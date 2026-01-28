using System.IO.Pipelines;

namespace FileSorting.Generator;

public sealed class FileGenerator(
    LineGenerator lineGenerator,
    IProgress<long>? progress = null)
{
    private const int FileStreamBuffer = 4 * 1024 * 1024; // 4MB
    private const int PipeBuffer = 8 * 1024; // 8KB

    // Max bytes a single line can occupy (number + ". " + string + newline)
    // estimate: 10 digits + 2 + ~500 char string + 1
    private const int MaxLineSize = 1024;

    public async Task GenerateAsync(
        string outputPath,
        long targetSize,
        CancellationToken cancellationToken = default)
    {
        // scale buffer sizes based on target file size
        var flushInterval = GetFlushInterval(targetSize);

        await using var fileStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: FileStreamBuffer,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var opts = new StreamPipeWriterOptions(leaveOpen: false, minimumBufferSize: PipeBuffer);
        var writer = PipeWriter.Create(fileStream, opts);

        var bytesWritten = 0L;
        var lastReported = 0L;

        try
        {
            while (bytesWritten < targetSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Get a larger buffer and write multiple lines
                var buffer = writer.GetMemory(PipeBuffer);
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

    private static long GetFlushInterval(long targetSize)
    {
        return targetSize switch
        {
            < 1 * 1024 * 1024 /* < 1MB */ => targetSize / 4,
            < 100 * 1024 * 1024 /* < 100MB */ => 10 * 1024 * 1024,
            < 1024L * 1024 * 1024 /* < 1GB */ => 50 * 1024 * 1024,
            _ => 100 * 1024 * 1024
        };
    }
}
