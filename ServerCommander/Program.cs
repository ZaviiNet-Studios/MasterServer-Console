using BIAB.WebAPI;
using Docker.DotNet;
using Microsoft.AspNetCore.Identity;
using ServerCommander.Data;
using ServerCommander.Services;

var builder = WebApplication.CreateBuilder(args);
ApiSettings settings = builder.AddSettings<ApiSettings>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// Add Swagger Options for Swagger Generation
builder.Services.AddSwaggerGen(c =>
{
    c.AddSwaggerJwtBearer(); // Adds the JWT Bearer authentication scheme to the Swagger UI.
});

// Add the DbContext
builder.Services.AddDbContext<ServerCommanderContext>();

// For Identity
builder.Services.AddBasicIdentity<IdentityUser, IdentityRole, ServerCommanderContext>();

// Adding Authentication
builder.Services.AddJwtAuthentication(settings);

var app = builder.Build();

// Development Only - Enables Swagger UI and Disable CORS
app.DevelopmentCorsAndSwaggerOverride();

// Adds Https Redirection
app.UseHttpsRedirection();

// Adds Authentication
app.UseJwtAuthentication();

app.MapControllers();

app.AutoMigrateDb<ServerCommanderContext>();

CancellationTokenSource cts = new CancellationTokenSource();


// Check if Docker is running via docker client
// If not running, throw exception
// If running, continue
bool isDockerRunning = false;
try{
    DockerClient client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();
    var info = client.System.GetSystemInfoAsync().Result;
    Console.WriteLine("Docker is running");
    isDockerRunning = true;
}
catch(Exception ex){
    Console.WriteLine("Error: Docker is not running!");
    cts.Cancel();
    isDockerRunning = false;
}

if (!isDockerRunning)
{
    Console.WriteLine("Please start Docker and try again");
    return;
}


Console.WriteLine("API is starting");
RunWebServer();
Console.WriteLine("Starting Game Server Service");
GameServerService.Main(cts);


async void RunWebServer()
{
    await app.RunAsync(cts.Token);
}
