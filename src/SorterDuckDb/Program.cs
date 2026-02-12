using FileSorting.SorterDuckDb;
using Spectre.Console.Cli;

var app = new CommandApp<DuckDbSortCommand>();

app.Configure(config =>
{
    config.SetApplicationName("sorter-duckdb");
});

return await app.RunAsync(args);
