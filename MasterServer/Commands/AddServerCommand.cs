using ServerCommander.Interfaces;

namespace ServerCommander.Commands;

public class AddServerCommand : IConsoleCommand
{
    public string Command => "addserver";
    public string[] Aliases => new string[] { "add" };
    public string Description => "Adds a server to the list of servers";
    public string Usage => "addserver <server name:optional> <server :optional> <server port:optional>";
    public async Task ExecuteAsync(string[] args)
    {
        string? serverName = (args.Length>= 1) ? args[0] : null;
        string ipaddress = String.Empty;
        int port = 0;

        while (string.IsNullOrEmpty(serverName))
        {
            TFConsole.WriteLine("Please enter a name for the server");
            string addServerName = Console.ReadLine() ?? "";
            if(!string.IsNullOrEmpty(addServerName))
            {
                serverName = addServerName;
            }else
            {
                TFConsole.WriteLine("Please enter a valid name for the server");
            }
        }
        
        while (string.IsNullOrEmpty(ipaddress))
        {
            TFConsole.WriteLine("Enter the IP address of the game server:");
            string addIpAddress = Console.ReadLine() ?? "";
            if (!string.IsNullOrEmpty(addIpAddress))
            {
                ipaddress = addIpAddress;
            }else
            {
                ipaddress = null;
            }
        }

        while (string.IsNullOrEmpty(ipaddress))
        {
            TFConsole.WriteLine("Enter the port of the game server:");
            int addPort = int.Parse(Console.ReadLine() ?? "");
            if (addPort > 0)
            {
                port = addPort;
            }
            else
            {
                TFConsole.WriteLine("Port cannot be empty or 0");
            }
        }

        Program.CreateGameServers(ipaddress, port, 0, false);

        TFConsole.WriteLine(
            $"Added game server at {ipaddress}:{port} with InstanceID");
        await Task.CompletedTask;
    }
}