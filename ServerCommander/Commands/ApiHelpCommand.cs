using ServerCommander.Interfaces;

namespace ServerCommander.Commands;

public class ApiHelpCommand : IConsoleCommand
{
    public string Command => "apihelp";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "Displays a list of available api endpoints";
    public string Usage => "apihelp";
    public Task ExecuteAsync(string[] args)
    {
        TFConsole.WriteLine("API Help");
        TFConsole.WriteLine(
            "/connect?partySize=*PartySize* - Connects to a game server with the specified party size eg. /connect?partySize=4");
        TFConsole.WriteLine("/list-servers - Lists all available game servers");
        TFConsole.WriteLine("/show-full-servers - Lists all full game servers");
        TFConsole.WriteLine("/add - Adds a new game server to the list");
        return Task.CompletedTask;
    }
}