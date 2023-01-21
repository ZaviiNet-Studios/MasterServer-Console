using ServerCommander.Data.Entities;
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
        foreach (ServerInstance server in GameServerService.GetServerInstances())
        {
            TFConsole.WriteLine(
                $"[{server.ServerId}] {server.PublicIpAddress}:{server.Port} ({server.PlayerCount}/{server.MaxCapacity})");
        }
        return Task.CompletedTask;
    }
}