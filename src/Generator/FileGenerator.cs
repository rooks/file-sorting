using System.Buffers;
using System.IO.Pipelines;

namespace FileSorting.Generator;

public sealed class FileGenerator(
    LineGenerator lineGenerator,
    IProgress<long>? progress = null)
{
    private const int ReportInterval = 10 * 1024 * 1024; // 10MB

    public async Task GenerateAsync(
        string outputPath,
        long targetSize,
        CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var writer = PipeWriter.Create(fileStream);

        var bytesWritten = 0L;
        var lastReported = 0L;

        try
        {
            while (bytesWritten < targetSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Get buffer from PipeWriter
                var buffer = writer.GetMemory(1024); // Request at least 1KB

                // Write a line
                var lineBytes = lineGenerator.WriteLine(buffer.Span);
                writer.Advance(lineBytes);
                bytesWritten += lineBytes;

                // Flush periodically to avoid unbounded memory growth
                if (bytesWritten - lastReported >= ReportInterval)
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
}
