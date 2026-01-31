using FileSorting.Sorter;
using Spectre.Console.Cli;

var app = new CommandApp<SortCommand>();
app.Configure(config =>
{
    config.SetApplicationName("sorter");
});

return await app.RunAsync(args);
