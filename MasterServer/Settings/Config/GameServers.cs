namespace MasterServer;

public class GameServers
{
    public GameServers(int numServers = default)
    {
        this.numServers = numServers;
    }

    public int numServers { get; set; }
}