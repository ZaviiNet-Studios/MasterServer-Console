using ServerCommander.Interfaces;
using ServerCommander.Settings.Config;

namespace ServerCommander.Commands;

public class CreateStandyServerCommand : IConsoleCommand
{
    public string Command => "CreateStandbyserver";
    public string[] Aliases => new string[] { "css" };
    public string Description => "Creates a Standby Server";
    public string Usage => "cbs or CreateBasicServer will create a server with default settings with standby enabled";
    private static readonly MasterServerSettings Settings = MasterServerSettings.GetFromDisk();

    private string ip = Settings.MasterServerIp;

    public async Task ExecuteAsync(string[] args)
    {
        Program.CreateGameServers(ip,null,0,true);
        TFConsole.WriteLine("Standby Server Created");
    }
}