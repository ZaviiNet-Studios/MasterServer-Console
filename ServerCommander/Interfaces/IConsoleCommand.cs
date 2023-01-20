namespace ServerCommander.Interfaces;

public interface IConsoleCommand
{
    public string Command { get; }
    public string[] Aliases { get; }
    public string Description { get; }
    public string Usage { get; }

    public Task ExecuteAsync(string[] args);
}