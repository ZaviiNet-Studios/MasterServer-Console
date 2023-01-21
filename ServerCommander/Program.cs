using BIAB.WebAPI;
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

Console.WriteLine("API is starting");
RunWebServer();
Console.WriteLine("Starting Game Server Service");
GameServerService.Main(cts);

async void RunWebServer()
{
    await app.RunAsync(cts.Token);
}
