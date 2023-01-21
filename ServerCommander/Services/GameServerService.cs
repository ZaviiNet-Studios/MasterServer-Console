using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Docker.DotNet;
using Docker.DotNet.Models;
using Newtonsoft.Json;
using PlayFab;
using ServerCommander.Commands;
using ServerCommander.Data;
using ServerCommander.Data.Entities;
using ServerCommander.Data.Enums;
using ServerCommander.Data.Repositories;
using ServerCommander.Lib.Modal;
using ServerCommander.Settings.Config;

namespace ServerCommander.Services;

public class GameServerService
{
    private static int _numServers;
    public static readonly MasterServerSettings Settings = MasterServerSettings.GetFromDisk();
    private static readonly int Port = Settings.MasterServerPort;
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

    private static ServerCommanderContext Context { get; set; } = new();
    
    private static ServerInstanceRepository ServerInstanceRepository { get; set; } = new(Context);
    
    public static List<ServerInstance> GetServerInstances() => ServerInstanceRepository.Get().ToList();

    public static bool UpdateServerPlayerCount(string serverId, int playerCount)
    {
        ServerInstance? instance = ServerInstanceRepository.GetByServerId(serverId);
        
        if (instance == null) return false;
        
        instance.UpdatePlayerCountOverride(playerCount);
        
        Context.SaveChanges();
        
        return true;
    } 
    public static bool UpdateServerPlayerCount(int serverId, int playerCount)
    {
        ServerInstance? instance = ServerInstanceRepository.GetByPort(serverId);
        
        if (instance == null) return false;
        
        instance.UpdatePlayerCountOverride(playerCount);
        
        Context.SaveChanges();
        
        return true;
    }

    public static async Task RemoveServer(int port)
    {
        ServerInstance? instance = await ServerInstanceRepository.GetByPortAsync(port);
        
        if (instance == null) return;
        
        if(!string.IsNullOrWhiteSpace(instance.DockerInstanceId))
            await DockerService.DeleteDockerContainerByPort(instance.DockerInstanceId);
        
        ServerInstanceRepository.Delete(instance);
    }

    private static CancellationTokenSource? programCTS = null;
    public static async void Main(CancellationTokenSource cts)
    {
        programCTS = cts;
        Startup();

        ListenForServersThread = new Thread(() =>
        {
            ListenForServers(ListenForServersCancellationToken.Token);
        });
        CheckForEmptyServersThread = new Thread(() =>
        {
            CheckForEmptyServers(CheckForEmptyServersCancellationToken.Token);
        });

        ListenForServersThread.Start();
        CheckForEmptyServersThread.Start();
        
        CreateInitialGameServers(null, null);

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

    private static void CreateInitialGameServers(string? ip, int? port)
    {
        if (!_isRunning)
        {
            TFConsole.WriteLine("Docker is not running, Unable to create initial game servers", ConsoleColor.Red);
            return;
        }

        int gameServersToBeCreated = Settings.NumberOfInitialGameServers;
        int gameServersCreated = Context.ServerInstances.Count();
        if (!Settings.CreateInitialGameServers) return;

        for (int i = 0; i < gameServersToBeCreated; i++)
        {
            try
            {
                CreateDockerContainer(ip, port, out string InstancedID, out string serverID);
                CreateNewServer(ip, port, InstancedID, serverID, true);
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

    public static void CreateGameServers(string ip, int port, bool isStandby)
    {
        try
        {
            CreateDockerContainer( ip, port, out string InstancedID, out string serverID);
            CreateNewServer( ip, port, InstancedID, serverID, isStandby);
        }
        catch (Exception ex)
        {
            TFConsole.WriteLine(ex.Message, ConsoleColor.Red);
        }
    }



    //Server Creation Stuff
    public static ServerInstance CreateNewServer( string? ip, int? port, string InstancedID,
        string serverID, bool isStandby)
    {
        // Use the provided IP but fallback to DefaultIP if provided is null
        string serverIP = ip ?? DefaultIp ?? "0.0.0.0";
        int serverPort = _portPool;

        if (port != null)
            serverPort = port.Value;

        ServerInstance gameServer = new ServerInstance
        {
            PublicIpAddress = serverIP,
            PrivateIpAddress = serverIP,
            Port = serverPort,
            DockerInstanceId = InstancedID,
            MaxCapacity = Settings.MaxPlayersPerServer,
            ServerId = serverID,
            State = isStandby ? ServerState.Standby : ServerState.Ready
        };

        ServerInstanceRepository.Add(gameServer);
        _portPool++;
        return gameServer;
    }

    public static void CreateDockerContainer(string? ip, int? port,
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
                if (ServerInstanceRepository.Get().Count() < Settings.MaxGameServers)
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

    private static void CheckForEmptyServers(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Sleep for 5 minutes
            Thread.Sleep(5 * 60 * 1000);

            List<ServerInstance> servers = ServerInstanceRepository.Get().ToList();
            
            // Check each game server in the list
            foreach (ServerInstance server in servers)
            {
                // If the server has 0 players, delete the container
                if (server.PlayerCount != 0) continue;
                if (server.State is not ServerState.Standby)
                {
                    _ = _dockerService.DeleteDockerContainer(server.DockerInstanceId);
                    TFConsole.WriteLine($"Server {server.DockerInstanceId} has been deleted", ConsoleColor.Yellow);
                    ServerInstanceRepository.Delete(server);
                }
                else
                {
                    TFConsole.WriteLine("Standby Server Detected, Not Deleting", ConsoleColor.Yellow);
                }
            }

            ServerInstanceRepository.SaveChanges();
        }
    }

    private static async void ListenForServers(CancellationToken cancellationToken)
    {
        // Create a TCP listener
        TcpListener listener = new(IPAddress.Any, Port);
        listener.Start();

        
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
            ServerInstance? gameServer = await ServerInstanceRepository.GetByServerIdAsync(serverId);
            if (gameServer == null)
            {
                TFConsole.WriteLine($"Received data from unknown game server: {serverId}", ConsoleColor.Red);
                continue;
            }
            
            gameServer.UpdatePlayerCount(playerCount);
            await Context.SaveChangesAsync(cancellationToken);
            
            TFConsole.WriteLine($"Received data from game server {serverId}: {playerCount} players",
                ConsoleColor.Green);

            // Close the connection with the game server
            client.Close();
        }
    }
}
