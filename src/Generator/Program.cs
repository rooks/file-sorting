using FileSorting.Generator;
using Spectre.Console.Cli;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

GenerateCommand.CancellationToken = cts.Token;

var app = new CommandApp<GenerateCommand>();
app.Configure(config =>
{
    config.SetApplicationName("generator");
});

return await app.RunAsync(args);
