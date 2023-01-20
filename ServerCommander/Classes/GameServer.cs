namespace ServerCommander.Classes;

public class GameServer
{
    public bool isActive {get; set;}
    public string ipAddress { get; set; }
    public int port { get; set; }
    public int playerCount { get; set; }
    public int maxCapacity { get; set; }
    public string ServerId { get; set; }
    public string instanceId { get; set; }
    
    public bool isStandby { get; set; }

    public GameServer(string ipAddress, int port, int playerCount, int maxCapacity, string instanceId, bool isActive, string serverId, bool isStandby)
    {
        this.ipAddress = ipAddress;
        this.port = port;
        this.playerCount = playerCount;
        this.maxCapacity = maxCapacity;
        this.instanceId = instanceId;
        this.isActive = isActive;
        this.ServerId = serverId;
        this.isStandby = isStandby;
    }

    public GameServer()
    {
        isActive = true;
        ipAddress = "";
        port = 0;
        playerCount = 0;
        maxCapacity = 0;
        instanceId = "";
        ServerId = "";
    }
    
    public string GetPopulation()
    {
        float population = (float)playerCount / (float)maxCapacity;

        return population switch
        {
            >= 0.8f => "High",
            >= 0.5f => "Medium",
            _ => "Low",
        };
    }
    
    public string GetStatus()
    {
        return isActive ? (playerCount == maxCapacity) ? "full":"active" : "offline";
    }
}