namespace ServerCommander.Data.Entities;

public class PlayerCountUpdate
{
    public int Id { get; set; }
    
    public int PlayerCount { get; set; }
    public bool FromServer { get; set; }
    
    public int ServerId { get; set; }
    public ServerInstance? Server { get; set; }
    
    public DateTime Time { get; set; } = DateTime.UtcNow;
}