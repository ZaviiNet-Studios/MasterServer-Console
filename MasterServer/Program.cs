using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Docker.DotNet;
using Docker.DotNet.Models;
using MasterServer;
using Newtonsoft.Json;
using PlayFab;
using ServerCommander.Classes;
using ServerCommander.Commands;
using ServerCommander.Services;
using ServerCommander.Settings.Config;

#pragma warning disable CS1998

namespace ServerCommander
{
    class Program
    {
        private static int _numServers;
        private static readonly MasterServerSettings Settings = MasterServerSettings.GetFromDisk();
        private static readonly GameServers? InitialServers = InitialDockerContainerSettings();
        private static readonly int Port = Settings.MasterServerPort;
        private static readonly int WebPort = Settings.MasterServerApiPort;
        private static readonly string? DefaultIp = Settings.MasterServerIp;
        private static int _portPool = Settings.GameServerPortPool;
        private static string _networkString = "127.0.0.1";
        private static bool _isRunning = true;

        /// <summary>
        /// Stops The Main Loop When False
        /// </summary>
        private static bool MainThreadRunning { get; set; } = true;
        
        private static readonly HttpService _httpService = new HttpService(WebPort, Settings);
        private static readonly DockerService _dockerService = new DockerService(Settings);
        private static readonly DatabaseService _databaseService = new DatabaseService(Settings);
        
        public static DockerService DockerService => _dockerService;
        
        private static Thread ListenForServersThread { get; set; }
        private static readonly CancellationTokenSource  ListenForServersCancellationToken = new CancellationTokenSource ();
        private static Thread CheckForEmptyServersThread { get; set; }
        private static readonly CancellationTokenSource  CheckForEmptyServersCancellationToken = new CancellationTokenSource ();

        public static readonly CommandService CommandService = new CommandService();

        //private static readonly List<GameServer> Servers = _databaseService.Servers;
        
        public static List<GameServer> GetServers() => _databaseService.Servers;
        public static GameServer? GetServer(int port)
        {
            return _databaseService.Servers.FirstOrDefault(server => server.port == port);
        }
        public static void RemoveServer(int port)
        {
            GameServer? gameServer = GetServer(port);
            if (gameServer != null)
            {
                _databaseService.Servers.Remove(gameServer);
            }
        }
        
        public static async Task Main(string[] args)
        {
            Startup();
            
            ListenForServersThread = new Thread(() => { ListenForServers(_databaseService.Servers, ListenForServersCancellationToken.Token); });
            CheckForEmptyServersThread = new Thread(() => { CheckForEmptyServers(_databaseService.Servers, CheckForEmptyServersCancellationToken.Token); });
            
            ListenForServersThread.Start();
            _httpService.Start(_databaseService.Servers);
            CheckForEmptyServersThread.Start();
            
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
            CommandService.RegisterCommand(new CreateBasicServerCommand());
            CommandService.RegisterCommand(new CreateStandyServerCommand());
        }

        private static void Startup()
        {
            TFConsole.Start();
            TFConsole.WriteLine("Loading ServerCommander\n", ConsoleColor.Green);
            TFConsole.WriteLine($"Starting {Settings.MasterServerName}...\n", ConsoleColor.Green);
            TFConsole.WriteLine($"Send POST Data To http://{Settings.MasterServerIp}:{Port}\n", ConsoleColor.Green);
            TFConsole.WriteLine("Waiting for Commands... type 'help' to get a list of commands\n", ConsoleColor.Green);
            TFConsole.WriteLine("Type Quit or Exit to Close Application.", ConsoleColor.Green);
            if (!Settings.DatabaseCreated)
            {
                TFConsole.WriteLine("Creating Database...", ConsoleColor.Green);
                _ = _databaseService.CreateDatabaseAndTable();
            }
            else
            {
                _ = _databaseService.LoadGameServersAsync();
                TFConsole.WriteLine("Database Loading...", ConsoleColor.Green);
            }
            _ = CheckDatabaseAgainstRunningServers(_databaseService.Servers);
            TFConsole.WriteLine("Done!", ConsoleColor.Green);
        }


        private static async Task CheckDatabaseAgainstRunningServers(List<GameServer> gameServers)
        {
            var endpointUrl = $"{Settings.DockerTcpNetwork}";

            // Create a new DockerClient using the endpoint URL
            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();

            var containers = client.Containers.ListContainersAsync(new ContainersListParameters()
            {
                All = true
            }).Result;

            if (containers.Count == 0)
            {
                foreach (var container in containers)
                {
                    if (!container.Names.Any(name => name.Contains("GameServer")))
                    {
                        TFConsole.WriteLine("No GameServers Found", ConsoleColor.Red);
                        gameServers.Clear();
                        _ = _databaseService.RemoveAllGameServers();
                        CreateInitialGameServers(_databaseService.Servers, null, null, 0);
                        return;
                    }
                }
                TFConsole.WriteLine("No Docker Containers Found", ConsoleColor.Red);
                gameServers.Clear();
                _ = _databaseService.RemoveAllGameServers();
                CreateInitialGameServers(_databaseService.Servers, null, null, 0);
                TFConsole.WriteLine("Created Initial GameServers", ConsoleColor.Green);
            }
            
            try
            {
                TFConsole.WriteLine("Checking Database Against Running Servers...", ConsoleColor.Green);
                foreach (var container in containers)
                {
                    var instanceId = container.ID;

                    if (!container.Names.Any(name => name.Contains("GameServer")) || container.State != "running")
                    {
                        continue;
                    }

                    var gameServer = gameServers.FirstOrDefault(server => server.instanceId == instanceId);

                    if (gameServer == null)
                    {
                        if (Settings.AllowServerDeletion)
                        {
                            TFConsole.WriteLine($"Deleting Game Server {instanceId}", ConsoleColor.Green);
                            _ = _dockerService.DeleteDockerContainerByID(instanceId);
                        }
                    }
                    else
                    {
                        var containerPort = gameServer.port;
                        TFConsole.WriteLine(
                            $"Found Container: {container.Names.First()} with ID: {instanceId} and port {containerPort}, Recreating Server",
                            ConsoleColor.Green);
                        _ = _dockerService.DeleteDockerContainerByID(instanceId);
                        _ = _databaseService.RemoveAllGameServers();
                        gameServers.Remove(gameServer);
                        ReCreateServer(_databaseService.Servers, container.Names.First(), null, containerPort, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                TFConsole.WriteLine(ex.Message, ConsoleColor.Red);
            }
            
            if (containers.Count == 1)
            {
                TFConsole.WriteLine("Number of Standby Servers is less than the number of Standby Servers in the Database, ReCreating All Servers", ConsoleColor.Green);
                _ = _databaseService.RemoveAllGameServers();
                gameServers.Clear();
                _ = _dockerService.DeleteExistingDockerContainers();
                CreateInitialGameServers(_databaseService.Servers, null, null, 0);
            }
    }

        public static void Quit()
        {
            // Stop Main Thread Loop
            MainThreadRunning = false;
            _isRunning = false;
            
            // Stop Running Threads
            ListenForServersCancellationToken.Cancel();
            CheckForEmptyServersCancellationToken.Cancel();
            ListenForServersThread.Join();
            CheckForEmptyServersThread.Join();
            
            // Stop All Docker Containers
            _ = _dockerService.StopAllDockerContainers();
            
            // Stop Http Service
            _httpService.Stop();
            
            // Save Current Settings To File
            Settings.SaveToDisk();
            
            Environment.Exit(0);
        }

        public static void ReCreateServer(List<GameServer> gameServers, string Name, string ip, int? port, int partySize)
        {
            string InstancedID;
            string serverID;
            
            CreateDockerContainer(gameServers, Name,ip, port, out InstancedID, out serverID);
            CreateNewServer(gameServers, ip, port, InstancedID, serverID, true);
            TFConsole.WriteLine("Game server created successfully", ConsoleColor.Green);
        }

        private static void CreateInitialGameServers(List<GameServer> gameServers, string ip, int? port,
            int partySize)
        {
            if (_isRunning)
            {

                var gameServersToBeCreated = InitialServers.numServers;
                var gameServersCreated = 0;
                string InstancedID;
                string serverID;
                if (!Settings.CreateInitialGameServers) return;
                while (true)
                {
                    if (gameServersCreated < gameServersToBeCreated)
                    {
                        CreateDockerContainer(gameServers, null,ip, port, out InstancedID, out serverID);
                        CreateNewServer(gameServers, ip, port, InstancedID, serverID, true);
                        gameServersCreated++;
                    }
                    else
                    {
                        TFConsole.WriteLine(
                            $"Initial game servers created successfully - Number Created = {0} {gameServersCreated}",ConsoleColor.Green);
                        break;
                    }
                }
            }
            else
            {
                TFConsole.WriteLine("Docker is not running, Unable to create initial game servers",ConsoleColor.Red);
            }
        }

        public static void CreateGameServers(string ip, int? port, int partySize, bool isStandby)
        {
            var gameServersToBeCreated = InitialServers.numServers;
            var InstancedID = String.Empty;
            string serverID;
            

            CreateDockerContainer(_databaseService.Servers, null,ip, port, out InstancedID, out serverID);
            CreateNewServer(_databaseService.Servers, ip, port, InstancedID, serverID, isStandby);
        }

        private static GameServers? InitialDockerContainerSettings()
        {
            var filepath = "config/initialGameServers.json";
            if (!File.Exists(filepath))
            {
                GameServers initialSettings = new GameServers
                {
                    numServers = 2
                };
                var json = JsonConvert.SerializeObject(initialSettings, Formatting.Indented);

                Directory.CreateDirectory("config");
                File.WriteAllText(filepath, json);
                return initialSettings;
            }
            else
            {
                var json = File.ReadAllText(filepath);
                return JsonConvert.DeserializeObject<GameServers>(json);
            }
        }
        //Server Creation Stuff
        public static GameServer CreateNewServer(List<GameServer> gameServers, string ip, int? port,
            string InstancedID, string serverID, bool isStandby)
        {
            var serverIP = DefaultIp;
            var serverPort = _portPool;
            if (!string.IsNullOrEmpty(ip))
            {
                serverIP = ip;
            }

            if (port != null)
                serverPort = port.Value;

            var gameServer = new GameServer(serverIP, serverPort, 0, Settings.MaxPlayersPerServer, InstancedID, true,
                serverID, isStandby);

            _ = _databaseService.SaveGameServer(serverIP, serverPort, 0, Settings.MaxPlayersPerServer,InstancedID, true,
                serverID, isStandby);

            gameServers.Add(gameServer);
            _portPool++;
            Settings.SaveToDisk();
            return gameServer;
        }

        public static void CreateDockerContainer(List<GameServer> gameServers, string? Name, string? ip, int? port,
            out string InstancedID, out string ServerID)
        {
            
            var imageName = $"{Settings.DockerContainerImage}";
            var imageTag = $"{Settings.DockerContainerImageTag}";
            var endpointUrl = $"{Settings.DockerTcpNetwork}";

            var randomGuid = Guid.NewGuid().ToString();
            ServerID = randomGuid;
            var newInstancedID = string.Empty;
            var HostIP = "0.0.0.0";
            var HostPort = _portPool;

            if (!string.IsNullOrEmpty(ip))
            {
                HostIP = ip;
            }
            
            if(port != null)
                HostPort = port.Value;

            Guid guid = Guid.NewGuid();
            string first5Characters = guid.ToString().Substring(0, 5);
            Name ??= $"GameServer-Instance--{first5Characters}";

            try
            {
                TFConsole.WriteLine($"New Server Requested with IP {HostIP} and Port {HostPort}", ConsoleColor.Yellow);

                if (Settings.AllowServerCreation)
                {
                    if (gameServers.Count < Settings.MaxGameServers)
                    {
                        // Create a new DockerClient using the endpoint URL
                        var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();
                        var host = Dns.GetHostName();
                        var addresses = Dns.GetHostAddresses(host);
                        var ipv4Address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

                        // Create a new container using the image name and tag, and the specified command
                        var createResponse = client.Containers.CreateContainerAsync(
                            new CreateContainerParameters
                            {
                                Image = imageName + ":" + imageTag,
                                Name = Name,
                                Hostname = HostIP,
                                Env = new List<string>
                                {
                                    $"Server-ID={randomGuid}",
                                    $"IP-Address={ipv4Address}"
                                },
                                ExposedPorts = new Dictionary<string, EmptyStruct>
                                {
                                    { "7777/udp", default(EmptyStruct) }
                                },
                                HostConfig = new HostConfig
                                {
                                    PortBindings = new Dictionary<string, IList<PortBinding>>
                                    {
                                        {
                                            "7777/udp",
                                            new List<PortBinding> { new PortBinding { HostPort = HostPort + "/udp" } }
                                        },
                                    }
                                }
                            }).Result;

                        // Get the ID of the new container
                        var containerId = createResponse.ID;
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
                    TFConsole.WriteLine("Server creation is disabled" ,ConsoleColor.Red);
                }

                InstancedID = newInstancedID;
            }
            catch (Exception e)
            {
                TFConsole.WriteLine("Error Creating Server, Check Docker is Running/ Check Connection Settings are Correct" ,ConsoleColor.Red);
                TFConsole.WriteLine(e.Message);
                InstancedID = string.Empty;
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
                        _ = _databaseService.RemoveGameServerByInstanceID(server.instanceId);
                        gameServers.RemoveAll(server => server.playerCount == 0);
                        TFConsole.WriteLine($"Server {server.instanceId} has been deleted", ConsoleColor.Yellow);
                    }
                    else
                    {
                        TFConsole.WriteLine("Standby Server Detected, Not Deleting" ,ConsoleColor.Yellow);
                    }
                }
            }
        }

        private static async void ListenForServers(List<GameServer> gameServers,
            CancellationToken cancellationToken)
        {
            // Create a TCP listener
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for a game server to connect
                var client = listener.AcceptTcpClient();

                // Read the data sent by the game server
                var stream = client.GetStream();
                var reader = new StreamReader(stream);
                var data = reader.ReadToEnd();

                // Deserialize the JSON data into a dynamic object
                dynamic values = JsonConvert.DeserializeObject(data);
                
                var json = JsonConvert.DeserializeObject<Dictionary<string, string>>(data);
                Debug.WriteLine($"Received data from {json}");

                // Extract the server ID and player count from the dynamic object
                var serverID = values.serverID;
                var playerCount = values.playerCount;
                
                // Find the game server with the matching server ID
                var gameServer = gameServers.Find(server => server.ServerId == (string)values["serverID"]); 
                if (gameServer==null)
                {
                    TFConsole.WriteLine($"Received data from unknown game server: {serverID}",ConsoleColor.Red);
                    continue;
                }
                
                // Update the game server's player count
                gameServer.playerCount = playerCount;

                TFConsole.WriteLine($"Received data from game server {serverID}: {playerCount} players",ConsoleColor.Green);
                if (playerCount != 0)
                {
                    _ = _databaseService.UpdateGameServerPlayerNumbers(playerCount,serverID);
                }

                // Close the connection with the game server
                client.Close();
            }
        }
    }
}
