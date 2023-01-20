using ServerCommander.Classes;
using ServerCommander.Interfaces;
using ServerCommander.Services;

namespace ServerCommander.Commands;

public class ListCommand : IConsoleCommand
{
    public string Command => "list";
    public string[] Aliases => new string[] { "ls" };
    public string Description => "Lists all available game servers";
    public string Usage => "list";
    public Task ExecuteAsync(string[] args)
    {
        // List the available game servers
        TFConsole.WriteLine("Available game servers:");
        foreach (GameServer server in GameServerService.GetServers())
        {
            TFConsole.WriteLine(
                $"[{server.ServerId}] {server.ipAddress}:{server.port} ({server.playerCount}/{server.maxCapacity})");
        }
        return Task.CompletedTask;
    }
}