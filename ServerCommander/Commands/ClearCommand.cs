using ServerCommander.Interfaces;

namespace ServerCommander.Commands;

public class ClearCommand : IConsoleCommand
{
    public string Command => "clear";
    public string[] Aliases => new [] { "cls", "clr" };
    public string Description => "Clears the console";
    public string Usage => "clear";
    public Task ExecuteAsync(string[] args)
    {
        Console.Clear();
        return Task.CompletedTask;
    }
}