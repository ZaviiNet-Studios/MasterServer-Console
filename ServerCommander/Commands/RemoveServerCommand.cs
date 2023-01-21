using ServerCommander.Interfaces;
using ServerCommander.Services;

namespace ServerCommander.Commands;

public class RemoveServerCommand : IConsoleCommand
{
    public string Command => "remove";
    public string[] Aliases => new string[] { "rm" };
    public string Description => "Removes a server from the list";
    public string Usage => "remove <server port>";
    public async Task ExecuteAsync(string[] args)
    {
        int? port = (args.Length >= 1) ? int.Parse(args[0]) : null;
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

        // Remove the game server from the list
        await GameServerService.RemoveServer(port.Value);
        TFConsole.WriteLine($"Removed game server at port {port.Value}.");
    }
}