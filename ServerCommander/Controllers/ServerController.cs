using Microsoft.AspNetCore.Mvc;
using ServerCommander.Services;

namespace ServerCommander.Controllers;

[ApiController]
[Route("[controller]")]
public class ServerController : ControllerBase
{

    private readonly ILogger _logger;

    public ServerController(ILogger logger)
    {
        _logger = logger;
    }

    [HttpGet("show-full-servers")]
    public ActionResult ListFull()
    {
        var enumerable = GameServerService.GetServers().Where(s => s.playerCount == s.maxCapacity)
            .Select(x => new
            {
                x.ipAddress,
                x.port,
                x.playerCount,
                x.maxCapacity
            });
        return Ok(enumerable);
    }

    [HttpGet("list-servers")]
    public ActionResult List()
    {
        var enumerable = GameServerService.GetServers().Where(s => s.playerCount == s.maxCapacity)
            .Select(x => new
            {
                x.ipAddress,
                x.port,
                x.playerCount,
                x.maxCapacity
            });
        return Ok(enumerable);
    }
}