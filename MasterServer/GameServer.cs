namespace MasterServer;

public class GameServer
{
    public string ipAddress { get; set; }
    public int port { get; set; }
    public int playerCount { get; set; }
    public int maxCapacity { get; set; }
    
    public string instanceId { get; set; }

    public GameServer(string ipAddress, int port, int playerCount, int maxCapacity, string instanceId)
    {
        this.ipAddress = ipAddress;
        this.port = port;
        this.playerCount = playerCount;
        this.maxCapacity = maxCapacity;
        this.instanceId = instanceId;
    }

    public GameServer()
    {
        ipAddress = "";
        port = 0;
        playerCount = 0;
        maxCapacity = 0;
        instanceId = "";
    }
}