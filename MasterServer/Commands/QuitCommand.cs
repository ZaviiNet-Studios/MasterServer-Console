using ServerCommander.Interfaces;

namespace ServerCommander.Commands;

public class QuitCommand : IConsoleCommand
{
    public string Command => "quit";
    public string[] Aliases => new string[] { "exit" };
    public string Description => "Quits the application";
    public string Usage => "quit";
    public async Task ExecuteAsync(string[] args)
    {
        Program.Quit();
        await Task.CompletedTask;
    }
}