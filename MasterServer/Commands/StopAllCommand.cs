using ServerCommander.Interfaces;

namespace ServerCommander.Commands;

public class StopAllCommand : IConsoleCommand
{
    public string Command => "stopall";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Stops all servers";
    public string Usage => "stopall";
    public async Task ExecuteAsync(string[] args)
    {
        TFConsole.WriteLine("Stopping all game servers.");
        await Program.StopAllDockerContainers();
        TFConsole.WriteLine("Stopped all game servers.");
    }
}