using Microsoft.AspNetCore.Mvc;
using PlayFab;
using PlayFab.AdminModels;
using ServerCommander.Data;
using ServerCommander.Data.Entities;
using ServerCommander.Data.Repositories;
using ServerCommander.Models;
using ServerCommander.Services;
using ServerCommander.Settings.Config;

namespace ServerCommander.Controllers;

[ApiController]
[Route("[controller]")]
public class ServerController : ControllerBase
{

    private readonly ILogger<ServerController> _logger;
    private readonly MasterServerSettings _settings;
    private readonly ServerInstanceRepository _repo;

    public ServerController(ILogger<ServerController> logger, ServerCommanderContext context)
    {
        _logger = logger;
        _repo = new ServerInstanceRepository(context);
        _settings = GameServerService.Settings;
    }

    [HttpGet("show-full-servers")]
    public ActionResult ListFull()
    {
        var enumerable =_repo.Get().Where(s => s.PlayerCount == s.MaxCapacity)
            .Select(x => new
            {
                IpAddress = x.PublicIpAddress,
                x.Port,
                x.PlayerCount,
                x.MaxCapacity
            });
        return Ok(enumerable);
    }

    [HttpGet("list-servers")]
    public ActionResult List()
    {
        var enumerable = _repo.Get()
            .Select(x => new
            {
                IpAddress = x.PublicIpAddress,
                x.Port,
                x.PlayerCount,
                x.MaxCapacity
            });
        return Ok(enumerable);
    }

    [HttpGet("servers.html")]
    public ActionResult ServersHtml()
    {
        var fileContents = System.IO.File.ReadAllText("./Content/servers.html");
        return new ContentResult()
        {
            Content = fileContents,
            ContentType = "text/html"
        };
    }
    
    [HttpPost("{serverId}/update-player-count")]
    public ActionResult UpdatePlayerCount(string serverId, [FromBody] UpdatePlayerCountRequest request)
    {
        var worked = GameServerService.UpdateServerPlayerCount(serverId, request.PlayerCount);
        if (worked)
        {
            return Ok();
        }else
        {
            return BadRequest();
        }
    }

    [HttpGet("admin-panel")]
    public ActionResult AdminList()
    {
        var enumerable = _repo.Get().Select(x => new
        {
            x.ServerId,
            IpAddress = x.PublicIpAddress,
            x.Port,
            x.PlayerCount,
            x.MaxCapacity,
            status = x.GetStatus(),
            population = x.GetPopulation()
        });
        return Ok(enumerable);
    }

    [HttpGet("connect")]
    public ActionResult Connect([FromQuery] int partySize, [FromQuery] string playfabId)
    {
        if (!_settings.AllowServerJoining)
        {
            TFConsole.WriteLine("Server joining is disabled", ConsoleColor.Yellow);
            return Ok("Server joining is disabled");
        }

        TFConsole.WriteLine($"Request with party size: {partySize} {playfabId}");
        
        // Validate token with PlayFab
        var isPlayerBanned = ValidateRequest(playfabId);

        if (!isPlayerBanned)
        {
            TFConsole.WriteLine("Player is banned", ConsoleColor.Red);
            return Ok("Player is banned");
        }

        var gameServers = _repo.Get();
        
        var availableServer = GetAvailableServer(partySize);
        if (availableServer != null)
        {
            TFConsole.WriteLine(
                $"Party of size {partySize} is assigned to : {availableServer.PublicIpAddress}:{availableServer.Port} InstanceID:{availableServer.DockerInstanceId} Player Count is {availableServer.PlayerCount}",
                ConsoleColor.Green);

            availableServer.UpdatePlayerCountOverride(availableServer.PlayerCount + partySize);
            _repo.SaveChanges();
            return Ok(new
            {
                ipAddress = availableServer.PublicIpAddress,
                port = availableServer.Port,
                serverId = availableServer.ServerId,
                playerCount = availableServer.PlayerCount,
            });
        }
        else
        {
            try
            {
                string serverID;
                GameServerService.CreateDockerContainer(string.Empty, null, out string instancedID,
                    out serverID);
                ServerInstance? newServer = GameServerService.CreateNewServer(string.Empty, null,
                    instancedID, serverID, false);
                return Ok(new
                {
                    ipAddress = newServer.PublicIpAddress,
                    port = newServer.Port,
                    playerCount = newServer.PlayerCount,
                    maxCapacity = newServer.MaxCapacity,
                    instancedID = newServer.DockerInstanceId,
                });
            }
            catch (Exception e)
            {
                TFConsole.WriteLine(e.Message, ConsoleColor.Red);
                return Ok("No servers available");
            }
        }
    }

    private ServerInstance? GetAvailableServer(int partySize)
    {
        // Check if there are any servers in the list
        if (!_repo.Get().Any())
        {
            // If no servers, return null
            return null;
        }
        
        return _repo.Get().Where(s => s.PlayerCount + partySize <= s.MaxCapacity).OrderBy(s=>s.PlayerCount).FirstOrDefault();
    }


    private bool ValidateRequest(string playfabID)
    {
        if (!_settings.UsePlayFab) return true;
        var adminAPISettings = new PlayFabApiSettings()
        {
            TitleId = _settings.PlayFabTitleID,
            DeveloperSecretKey = _settings.DeveloperSecretKey
        };

        var authenticationApi = new PlayFabAdminInstanceAPI(adminAPISettings);
        
        TFConsole.WriteLine("Validating Player " + playfabID);
        
        var request = new GetUserBansRequest()
        {
            PlayFabId = playfabID
        };

        Task<PlayFabResult<GetUserBansResult>> task = authenticationApi.GetUserBansAsync(request);
        task.Wait();

        var response = task.Result;


        var isBanned = response.Result.BanData.Count;

        TFConsole.WriteLine($"Player has {isBanned} Ban(s) on Record");

        return isBanned <= 0;
    }
}