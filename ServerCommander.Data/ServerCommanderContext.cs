using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServerCommander.Data.Entities;

namespace ServerCommander.Data;

public class ServerCommanderContext : IdentityDbContext
{
    public DbSet<ServerInstance> ServerInstances => Set<ServerInstance>();
    public DbSet<PlayerCountUpdate> PlayerCountUpdates => Set<PlayerCountUpdate>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseSqlite("Data Source=ServerCommander.db");
    }
}