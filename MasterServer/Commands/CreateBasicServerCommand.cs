using ServerCommander.Interfaces;
using ServerCommander.Settings.Config;

namespace ServerCommander.Commands;

public class CreateBasicServerCommand : IConsoleCommand
{
    public string Command => "CreateBasicServer";
    public string[] Aliases => new string[] { "cbs" };
    public string Description => "Creates a basic server with defaults";
    public string Usage => "cbs or CreateBasicServer will create a server with default settings";
    
    private static readonly MasterServerSettings Settings = MasterServerSettings.GetFromDisk();

    private string ip = Settings.MasterServerIp;
    public async Task ExecuteAsync(string[] args)
    {
        Program.CreateGameServers(ip,null,0,false);
        TFConsole.WriteLine("Server Created");
    }
}