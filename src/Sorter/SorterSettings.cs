using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FileSorting.Sorter;

public sealed class SorterSettings : CommandSettings
{
    [CommandOption("-i|--input")]
    [Description("Input file to sort")]
    public string? Input { get; init; }

    [CommandOption("-o|--output")]
    [Description("Output file path")]
    public string? Output { get; init; }

    [CommandOption("-t|--temp-dir")]
    [Description("Temporary directory for chunks")]
    public string? TempDir { get; init; }

    [CommandOption("-c|--chunk-size")]
    [Description("Chunk size (e.g., 64MB, 128MB)")]
    public string? ChunkSize { get; init; }

    [CommandOption("-p|--parallel")]
    [Description("Degree of parallelism")]
    public int? Parallel { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Input))
        {
            return ValidationResult.Error("--input is required");
        }

        if (!File.Exists(Input))
        {
            return ValidationResult.Error($"Input file not found: {Input}");
        }

        if (string.IsNullOrWhiteSpace(Output))
        {
            return ValidationResult.Error("--output is required");
        }

        if (TempDir != null && !Directory.Exists(TempDir))
        {
            return ValidationResult.Error($"Temp directory not found: {TempDir}");
        }

        return ValidationResult.Success();
    }
}
