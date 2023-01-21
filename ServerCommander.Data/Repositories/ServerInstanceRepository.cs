using Microsoft.EntityFrameworkCore;
using ServerCommander.Data.Entities;

namespace ServerCommander.Data.Repositories;

public class ServerInstanceRepository
{
    private readonly ServerCommanderContext _context;

    public ServerInstanceRepository(ServerCommanderContext context)
    {
        _context = context;
    }
    
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
    
    public IQueryable<ServerInstance> Get()
    {
        return _context.ServerInstances.Where(x=> x.DateDeleted == null);
    }

    public ServerInstance? GetByPort(int port)
    {
        return Get().FirstOrDefault(x => x.Port == port);
    }
    
    public async Task<ServerInstance?> GetByPortAsync(int port)
    {
        return await Get().FirstOrDefaultAsync(x => x.Port == port);
    }
    
    public ServerInstance? GetByServerId(string serverId)
    {
        return Get().FirstOrDefault(x => x.ServerId == serverId);
    }
    
    public async Task<ServerInstance?> GetByServerIdAsync(string serverId)
    {
        return await Get().FirstOrDefaultAsync(x => x.ServerId == serverId);
    }

    public void Delete(ServerInstance instance)
    {
        instance.DateDeleted = DateTime.UtcNow;
        _context.ServerInstances.Update(instance);
        
        _context.SaveChanges();
    }

    public void Add(ServerInstance gameServer)
    {
        _context.ServerInstances.Add(gameServer);
        _context.SaveChanges();
    }

    public void SaveChanges()
    {
        _context.SaveChanges();
    }
}