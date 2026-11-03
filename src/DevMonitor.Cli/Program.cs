using System.CommandLine;
using System.ComponentModel;
using System.Text;
using DevMonitor.Cli;

Console.OutputEncoding = Encoding.UTF8;

var root = new RootCommand("Debugging CLI for Dev Monitor. Source commands fetch live data and store nothing; "
    + "storage commands work with the app's database. The GitHub token can also be supplied through the "
    + "DEVMONITOR_GITHUB_TOKEN environment variable.");
foreach (var command in SourceCommands.Create().Concat(StorageCommands.Create()))
    root.Subcommands.Add(command);

try
{
    return await root.Parse(args).InvokeAsync(new InvocationConfiguration { EnableDefaultExceptionHandler = false });
}
catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or Win32Exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
