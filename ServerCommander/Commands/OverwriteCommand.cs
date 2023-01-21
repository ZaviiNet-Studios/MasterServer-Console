using ServerCommander.Interfaces;
using ServerCommander.Services;

namespace ServerCommander.Commands;

public class OverwriteCommand : IConsoleCommand
{
    public string Command => "overwrite";
    public string[] Aliases => new string[] { "ow" };
    public string Description => "Overwrites the player numbers settings";
    public string Usage => "overwrite <server port:optional> <player number:optional>";
    public Task ExecuteAsync(string[] args)
    {
        int? port = (args.Length >= 1) ? int.Parse(args[0]) : null;
        int? playerCount = (args.Length >= 2) ? int.Parse(args[1]) : null;
        while (port == null)
        {
            TFConsole.WriteLine("Enter the port of the game server:");
            int removePort = int.Parse(Console.ReadLine() ?? "");
            if (removePort > 0)
            {
                port = removePort;
            }
            else
            {
                TFConsole.WriteLine("Invalid port number");
            }
        }
        while (playerCount == null)
        {
            TFConsole.WriteLine("Enter the new player count:");
            int playerCountNew = int.Parse(Console.ReadLine() ?? "");
            if (playerCountNew > 0)
            {
                playerCount = playerCountNew;
            }
            else
            {
                TFConsole.WriteLine("Invalid player count");
            }
        }
        GameServerService.UpdateServerPlayerCount((int)port, (int)playerCount);
        return Task.CompletedTask;
    }
}