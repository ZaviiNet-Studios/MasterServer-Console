using Microsoft.AspNetCore.Mvc;

namespace ServerCommander.Controllers;

[ApiController]
public class PingController : ControllerBase
{
    [HttpGet("status")]
    public string Get()
    {
        return "Server is running";
    }
}