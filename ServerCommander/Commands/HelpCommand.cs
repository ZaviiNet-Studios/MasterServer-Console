using ServerCommander.Interfaces;
using ServerCommander.Services;

namespace ServerCommander.Commands;

public class HelpCommand : IConsoleCommand
{
    public string Command => "help";
    public string[] Aliases => new string[] { "h", "?" };
    public string Description => "Displays a list of commands and their descriptions.";
    public string Usage => "help [command:optional]";
    public async Task ExecuteAsync(string[] args)
    {
        List<IConsoleCommand> commandServiceCommands = GameServerService.CommandService.Commands;
        TFConsole.WriteLine("List of available commands:");
        
        if (args.Length == 0)
        {
            foreach (IConsoleCommand command in commandServiceCommands)
            {
                TFConsole.WriteLine($"{command.Command} - {command.Description}");
            }
        }
        else
        {
            IConsoleCommand? command = commandServiceCommands.FirstOrDefault(x => x.Command == args[0] || x.Aliases.Contains(args[0]));
            if (command == null)
            {
                TFConsole.WriteLine($"Command '{args[0]}' not found.");
            }
            else
            {
                TFConsole.WriteLine($"{command.Command} - {command.Description}");
                TFConsole.WriteLine($"Usage: {command.Usage}");
                TFConsole.WriteLine($"Aliases: {string.Join(", ", command.Aliases)}");
            }
        }
        await Task.CompletedTask;
    }
}