using ServerCommander.Data.Enums;

namespace ServerCommander.Data.Entities;

public class ServerInstance
{
    public int Id { get; set; }
    
    // Connection Data
    public string PublicIpAddress { get; set; }
    public string PrivateIpAddress { get; set; }
    public int Port { get; set; }
    public int MaxCapacity { get; set; }
    
    // Instance Data
    public string ServerId { get; set; }
    public string DockerInstanceId { get; set; }
    
    // State Data
    public int PlayerCount { get; set; }
    public ServerState State { get; set; }
    public DateTime? LastPing { get; set; }
    public List<PlayerCountUpdate> PlayerCountUpdates { get; set; } = new();


    public bool IsFull => PlayerCount >= MaxCapacity;
    public bool IsActive => State is ServerState.Ready or ServerState.Running or ServerState.Unresponsive;
    
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateDeleted { get; set; } = null;
    
    
    public void UpdatePlayerCount(int playerCount)
    {
        PlayerCount = playerCount;
        LastPing = DateTime.UtcNow;
        PlayerCountUpdates.Add(new PlayerCountUpdate
        {
            PlayerCount = playerCount,
            FromServer = true
        });
    }
    
    public void UpdatePlayerCountOverride(int playerCount)
    {
        PlayerCount = playerCount;
        PlayerCountUpdates.Add(new PlayerCountUpdate
        {
            PlayerCount = playerCount,
            FromServer = false
        });
    }
    
    public string GetPopulation()
    {
        float population = (float)PlayerCount / (float)MaxCapacity;

        return population switch
        {
            >= 0.8f => "High",
            >= 0.5f => "Medium",
            _ => "Low",
        };
    }

    public string GetStatus()
    {
        return IsActive ? (PlayerCount == MaxCapacity) ? "full":"active" : "offline";
    }
}