using FileSorting.Generator;
using Spectre.Console.Cli;

var app = new CommandApp<GenerateCommand>();
app.Configure(config =>
{
    config.SetApplicationName("generator");
});

return await app.RunAsync(args);
