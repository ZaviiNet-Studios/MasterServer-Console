using ServerCommander.Interfaces;

namespace ServerCommander.Services;

public class CommandService : IDisposable
{
    public List<IConsoleCommand> Commands { get; set; } = new List<IConsoleCommand>();
    

    public void RegisterCommand(IConsoleCommand command)
    {
        Commands.Add(command);
    }
    
    

    public async Task RunCommand(string? command, string[] args)
    {
        if (command == null)
        {
            return;
        }
        
        var consoleCommand = Commands.FirstOrDefault(x => x.Command == command || x.Aliases.Any(a => a == command));
        if (consoleCommand == null)
        {
            Console.WriteLine("Command not found");
            return;
        }

        await consoleCommand.ExecuteAsync(args);
    }

    public void Dispose()
    {
        Commands.Clear();
    }
}