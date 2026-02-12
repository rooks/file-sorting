using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FileSorting.SorterDuckDb;

public sealed class DuckDbSortSettings : CommandSettings
{
    [CommandOption("-i|--input")]
    [Description("Input file to sort")]
    public string? Input { get; init; }

    [CommandOption("-o|--output")]
    [Description("Output file path")]
    public string? Output { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Input))
            return ValidationResult.Error("--input is required");

        if (!File.Exists(Input))
            return ValidationResult.Error($"Input file not found: {Input}");

        if (string.IsNullOrWhiteSpace(Output))
            return ValidationResult.Error("--output is required");

        return ValidationResult.Success();
    }
}
