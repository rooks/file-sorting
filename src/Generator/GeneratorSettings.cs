using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FileSorting.Generator;

public sealed class GeneratorSettings : CommandSettings
{
    [CommandOption("-s|--size")]
    [Description("Target file size (e.g., 1GB, 500MB, 100KB)")]
    public string? Size { get; init; }

    [CommandOption("-o|--output")]
    [Description("Output file path")]
    public string? Output { get; init; }

    [CommandOption("-d|--dictionary")]
    [Description("Path to dictionary file (one word/phrase per line). If not specified, uses embedded default dictionary.")]
    public string? Dictionary { get; init; }

    [CommandOption("--seed")]
    [Description("Random seed for reproducible output")]
    public int? Seed { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Size))
            return ValidationResult.Error("--size is required");

        if (string.IsNullOrWhiteSpace(Output))
            return ValidationResult.Error("--output is required");

        if (Dictionary != null && !File.Exists(Dictionary))
            return ValidationResult.Error($"Dictionary file not found: {Dictionary}");

        return ValidationResult.Success();
    }
}
