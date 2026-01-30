using FileSorting.Sorter;
using Spectre.Console.Cli;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

SortCommand.CancellationToken = cts.Token;

var app = new CommandApp<SortCommand>();
app.Configure(config =>
{
    config.SetApplicationName("sorter");
});

return await app.RunAsync(args);
