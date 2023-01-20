using ServerCommander.Interfaces;
using ServerCommander.Services;

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
        await GameServerService.DockerService.StopAllDockerContainers();
        TFConsole.WriteLine("Stopped all game servers.");
    }
}