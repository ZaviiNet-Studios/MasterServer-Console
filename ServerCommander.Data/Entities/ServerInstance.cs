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
    
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    
    
    public void UpdatePlayerCount(int playerCount)
    {
        PlayerCount = playerCount;
        LastPing = DateTime.UtcNow;
        PlayerCountUpdates.Add(new PlayerCountUpdate
        {
            PlayerCount = playerCount
        });
    }
}