using ServerCommander.Interfaces;
using ServerCommander.Services;

namespace ServerCommander.Commands;

public class StartAllCommand : IConsoleCommand
{
    public string Command => "startall";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Starts all servers";
    public string Usage => "startall";
    public async Task ExecuteAsync(string[] args)
    {
        TFConsole.WriteLine("Starting all game servers.");
        await GameServerService.DockerService.StartAllDockerContainers();
        TFConsole.WriteLine("Started all game servers.");
    }
}