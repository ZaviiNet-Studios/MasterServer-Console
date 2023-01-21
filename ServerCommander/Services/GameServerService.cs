using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Docker.DotNet;
using Docker.DotNet.Models;
using MasterServer;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.AdminModels;
using ServerCommander.Classes;
using ServerCommander.Commands;
using ServerCommander.Data;
using ServerCommander.Data.Entities;
using ServerCommander.Lib.Modal;
using ServerCommander.Settings.Config;

namespace ServerCommander.Services;

public class GameServerService
{
    private static int _numServers;
    public static readonly MasterServerSettings Settings = MasterServerSettings.GetFromDisk();
    private static readonly GameServers? InitialServers = InitialDockerContainerSettings();
    private static readonly int Port = Settings.MasterServerPort;
    private static readonly int WebPort = Settings.MasterServerApiPort;
    private static readonly string? DefaultIp = Settings.MasterServerIp;
    private static int _portPool = Settings.GameServerPortPool;

    private static readonly string
        _networkString =
            "127.0.0.1"; //Change this to your network ip if you want to run the server on a different machine (If Docker is running then this is changed to the Bridge Network)

    private static bool _isRunning = true;

    /// <summary>
    /// Stops The Main Loop When False
    /// </summary>
    private static bool MainThreadRunning { get; set; } = true;

    private static readonly DockerService _dockerService = new(Settings);

    public static DockerService DockerService => _dockerService;

    private static Thread? ListenForServersThread { get; set; }
    private static readonly CancellationTokenSource ListenForServersCancellationToken = new();
    private static Thread? CheckForEmptyServersThread { get; set; }
    private static readonly CancellationTokenSource CheckForEmptyServersCancellationToken = new();

    public static readonly CommandService CommandService = new();
    private static readonly List<GameServer> Servers = new();

    public static List<GameServer> GetServers() => Servers;

    public static GameServer? GetServer(int port)
    {
        return Servers.FirstOrDefault(server => server.port == port);
    }
    
    public static GameServer? GetServer(string serverId)
    {
        return Servers.FirstOrDefault(server => server.ServerId == serverId);
    }

    public static void RemoveServer(int port)
    {
        GameServer? gameServer = GetServer(port);
        if (gameServer != null)
        {
            Servers.Remove(gameServer);
        }
    }

    static CancellationTokenSource programCTS = new CancellationTokenSource();
    public static async void Main(CancellationTokenSource cts)
    {
        programCTS = cts;
        Startup();

        ListenForServersThread = new Thread(() =>
        {
            ListenForServers(Servers, ListenForServersCancellationToken.Token);
        });
        CheckForEmptyServersThread = new Thread(() =>
        {
            CheckForEmptyServers(Servers, CheckForEmptyServersCancellationToken.Token);
        });

        ListenForServersThread.Start();
        CheckForEmptyServersThread.Start();


        var partySize = 0;
        CreateInitialGameServers(Servers, null, null, partySize);

        RegisterCommands();

        PlayFabAdminAPI.ForgetAllCredentials();
        while (MainThreadRunning)
        {
            // Check if the user has entered a command
            var command = Console.ReadLine() ?? "";

            // split the command into an array of strings
            var commandArgs = command.Split(' ');

            // Combine Segments if they are in quotes
            commandArgs = CombineQuotedSegments(commandArgs);

            await CommandService.RunCommand(commandArgs.FirstOrDefault(), commandArgs.Skip(1).ToArray());
        }
    }

    private static string[] CombineQuotedSegments(string[] commandArgs)
    {
        // Combine Segments if they are in quotes
        var combinedArgs = new List<string>();
        var currentArg = "";
        var inQuotes = false;

        foreach (var arg in commandArgs)
        {
            if (arg.StartsWith("\""))
            {
                inQuotes = true;
                currentArg += arg.Substring(1);
            }
            else if (arg.EndsWith("\""))
            {
                inQuotes = false;
                currentArg += " " + arg.Substring(0, arg.Length - 1);
                combinedArgs.Add(currentArg);
                currentArg = "";
            }
            else if (inQuotes)
            {
                currentArg += " " + arg;
            }
            else
            {
                combinedArgs.Add(arg);
            }
        }

        return combinedArgs.ToArray();
    }

    private static void RegisterCommands()
    {
        CommandService.RegisterCommand(new HelpCommand());
        CommandService.RegisterCommand(new QuitCommand());
        CommandService.RegisterCommand(new ClearCommand());
        CommandService.RegisterCommand(new AddServerCommand());
        CommandService.RegisterCommand(new StopAllCommand());
        CommandService.RegisterCommand(new StartAllCommand());
        CommandService.RegisterCommand(new ApiHelpCommand());
        CommandService.RegisterCommand(new OverwriteCommand());
        CommandService.RegisterCommand(new ListCommand());
        CommandService.RegisterCommand(new RemoveServerCommand());
    }

    private static void Startup()
    {
        TFConsole.Start();
        TFConsole.WriteLine("Loading ServerCommander\n", ConsoleColor.Green);
        TFConsole.WriteLine($"Starting {Settings.MasterServerName}...\n", ConsoleColor.Green);
        if (Settings.AllowServerDeletion)
        {
            _ = _dockerService.DeleteExistingDockerContainers();
        }

        TFConsole.WriteLine("Deleting existing Docker containers..., please wait", ConsoleColor.Green);
        TFConsole.WriteLine($"Send POST Data To http://{Settings.MasterServerIp}:{Port}\n", ConsoleColor.Green);
        TFConsole.WriteLine("Waiting for Commands... type 'help' to get a list of commands\n", ConsoleColor.Green);
        TFConsole.WriteLine("Type Quit or Exit to Close Application.", ConsoleColor.Green);
    }

    public static void Quit()
    {
        programCTS.Cancel();
        // Stop Main Thread Loop
        MainThreadRunning = false;
        _isRunning = false;

        // Stop Running Threads
        ListenForServersCancellationToken.Cancel();
        CheckForEmptyServersCancellationToken.Cancel();
        ListenForServersThread?.Join();
        CheckForEmptyServersThread?.Join();

        // Stop All Docker Containers
        _ = _dockerService.StopAllDockerContainers();

        // Save Current Settings To File
        Settings.SaveToDisk();

        Environment.Exit(0);
    }

    private static void CreateInitialGameServers(List<GameServer> gameServers, string? ip, int? port, int partySize)
    {
        if (_isRunning)
        {
            int gameServersToBeCreated = InitialServers?.numServers ?? 2;
            int gameServersCreated = 0;
            if (!Settings.CreateInitialGameServers) return;

            for (int i = 0; i < gameServersToBeCreated; i++)
            {
                try
                {
                    CreateDockerContainer(gameServers, ip, port, out string InstancedID, out string serverID);
                    CreateNewServer(gameServers, ip, port, InstancedID, serverID, true);
                    gameServersCreated++;
                }
                catch (Exception ex)
                {
                    TFConsole.WriteLine($"Failed to start server: {ex.Message}", ConsoleColor.Red);
                }
            }

            if (gameServersCreated > 0)
                TFConsole.WriteLine(
                    $"Initial game servers created successfully - Number Created = {gameServersCreated}",
                    ConsoleColor.Green);
            else
                TFConsole.WriteLine("Failed to create servers", ConsoleColor.Red);
        }
        else
        {
            TFConsole.WriteLine("Docker is not running, Unable to create initial game servers", ConsoleColor.Red);
        }
    }

    public static void CreateGameServers(string ip, int port, int partySize, bool isStandby)
    {
        // This is not implemented yet?
        //int gameServersToBeCreated = InitialServers?.numServers ?? 2;
        try
        {
            CreateDockerContainer(Servers, ip, port, out string InstancedID, out string serverID);
            CreateNewServer(Servers, ip, port, InstancedID, serverID, isStandby);
        }
        catch (Exception ex)
        {
            TFConsole.WriteLine(ex.Message, ConsoleColor.Red);
        }
    }

    private static GameServers? InitialDockerContainerSettings()
    {
        string filepath = "config/initialGameServers.json";
        if (!File.Exists(filepath))
        {
            GameServers initialSettings = new()
            {
                numServers = 2
            };
            string json = JsonConvert.SerializeObject(initialSettings, Formatting.Indented);

            Directory.CreateDirectory("config");
            File.WriteAllText(filepath, json);
            return initialSettings;
        }
        else
        {
            string json = File.ReadAllText(filepath);
            return JsonConvert.DeserializeObject<GameServers>(json);
        }
    }



    //Server Creation Stuff
    public static GameServer CreateNewServer(List<GameServer> gameServers, string? ip, int? port, string InstancedID,
        string serverID, bool isStandby)
    {
        // Use the provided IP but fallback to DefaultIP if provided is null
        string serverIP = ip ?? DefaultIp;
        int serverPort = _portPool;

        if (port != null)
            serverPort = port.Value;

        GameServer gameServer = new GameServer(serverIP ?? "0.0.0.0", serverPort, 0, Settings.MaxPlayersPerServer,
            InstancedID, true, serverID, isStandby);

        gameServers.Add(gameServer);
        _portPool++;
        return gameServer;
    }

    public static void CreateDockerContainer(List<GameServer> gameServers, string? ip, int? port,
        out string InstancedID, out string ServerID)
    {

        string imageName = $"{Settings.DockerContainerImage}";
        string imageTag = $"{Settings.DockerContainerImageTag}";
        string endpointUrl = $"{Settings.DockerTcpNetwork}";

        string randomGuid = Guid.NewGuid().ToString();
        ServerID = randomGuid;
        string newInstancedID = string.Empty;
        string hostIp = ip ?? "0.0.0.0";
        int hostPort = port ?? _portPool;


        try
        {
            TFConsole.WriteLine($"New Server Requested with IP {hostIp} and Port {hostPort}", ConsoleColor.Yellow);

            if (Settings.AllowServerCreation)
            {
                if (gameServers.Count < Settings.MaxGameServers)
                {
                    // Create a new DockerClient using the endpoint URL
                    DockerClient client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();
                    string host = Dns.GetHostName();
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    IPAddress? ipv4Address =
                        addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

                    // Create a new container using the image name and tag, and the specified command
                    var createResponse = client.Containers.CreateContainerAsync(
                        new CreateContainerParameters
                        {
                            Image = imageName + ":" + imageTag,
                            Name = $"GameServer-Instance--{_numServers}",
                            Hostname = hostIp,
                            Env = new List<string>
                            {
                                $"Server-ID={randomGuid}",
                                $"IP-Address={ipv4Address}"
                            },
                            ExposedPorts = new Dictionary<string, EmptyStruct>
                            {
                                { "7777/udp", default }
                            },
                            HostConfig = new HostConfig
                            {
                                PortBindings = new Dictionary<string, IList<PortBinding>>
                                {
                                    {
                                        "7777/udp",
                                        new List<PortBinding> { new PortBinding { HostPort = hostPort + "/udp" } }
                                    },
                                }
                            }
                        }).Result;

                    // Get the ID of the new container
                    string containerId = createResponse.ID;
                    newInstancedID = containerId;


                    // Start the new container
                    client.Containers.StartContainerAsync(containerId, null).Wait();
                    _numServers++;

                    TFConsole.WriteLine($"New Server Created with ID {ServerID}", ConsoleColor.Green);
                }
                else
                {
                    TFConsole.WriteLine("Max game servers reached", ConsoleColor.Red);
                }
            }
            else
            {
                TFConsole.WriteLine("Server creation is disabled", ConsoleColor.Red);
            }

            InstancedID = newInstancedID;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
            throw new Exception(
                "Error Creating Server, Check Docker is Running/ Check Connection Settings are Correct");
        }
    }

    private static void CheckForEmptyServers(List<GameServer> gameServers,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Sleep for 5 minutes
            Thread.Sleep(5 * 60 * 1000);

            // Check each game server in the list
            foreach (GameServer server in gameServers)
            {
                // If the server has 0 players, delete the container
                if (server.playerCount != 0) continue;
                if (!server.isStandby)
                {
                    _ = _dockerService.DeleteDockerContainer(server.instanceId);
                    TFConsole.WriteLine($"Server {server.instanceId} has been deleted", ConsoleColor.Yellow);
                    gameServers.RemoveAll(server => server.playerCount == 0);
                }
                else
                {
                    TFConsole.WriteLine("Standby Server Detected, Not Deleting", ConsoleColor.Yellow);
                }
            }
        }
    }

    private static async void ListenForServers(List<GameServer> gameServers, CancellationToken cancellationToken)
    {
        // Create a TCP listener
        TcpListener listener = new(IPAddress.Any, Port);
        listener.Start();

        ServerCommanderContext context = new();
        
        while (!cancellationToken.IsCancellationRequested)
        {
            // Wait for a game server to connect
            TcpClient client = listener.AcceptTcpClient();

            // Read the data sent by the game server
            NetworkStream stream = client.GetStream();
            StreamReader reader = new StreamReader(stream);
            string data = await reader.ReadToEndAsync(cancellationToken);

            // Deserialize the JSON data into a dynamic object
            UpdatePlayerCountModal? values = JsonConvert.DeserializeObject<UpdatePlayerCountModal>(data);

            dynamic json = JsonConvert.DeserializeObject<Dictionary<string, string>>(data);
            Debug.WriteLine($"Received data from {json}");

            if (values == null)
            {
                TFConsole.WriteLine("Client data was null! Aborting.");
                continue;
            }

            // Extract the server ID and player count from the dynamic object
            string serverId = values.ServerId;
            int playerCount = values.PlayerCount;

            // Find the game server with the matching server ID
            GameServer? gameServer = gameServers.Find(server => server.ServerId == serverId);
            if (gameServer == null)
            {
                TFConsole.WriteLine($"Received data from unknown game server: {serverId}", ConsoleColor.Red);
                continue;
            }

            ServerInstance? firstOrDefault = context.ServerInstances.FirstOrDefault(x => x.ServerId == serverId);
            if (firstOrDefault != null)
            {
                firstOrDefault.UpdatePlayerCount(playerCount);
                await context.SaveChangesAsync(cancellationToken);
            }
            // Update the game server's player count
            gameServer.playerCount = playerCount;

            TFConsole.WriteLine($"Received data from game server {serverId}: {playerCount} players",
                ConsoleColor.Green);

            // Close the connection with the game server
            client.Close();
        }
        await context.DisposeAsync();
    }
}
