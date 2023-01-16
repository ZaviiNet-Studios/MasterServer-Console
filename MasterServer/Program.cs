using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using MasterServer;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.AdminModels;
using ServerCommander.Classes;
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
        private static string _networkString = "127.0.0.1"; //Change this to your network ip if you want to run the server on a different machine (If Docker is running then this is changed to the Bridge Network)
        private static bool _isRunning = true;


        public static void Main(string[] args)
        {
            
            

            TFConsole.Start();

            TFConsole.WriteLine("Loading ServerCommander", ConsoleColor.Green);
            TFConsole.WriteLine();
            TFConsole.WriteLine($"Starting {Settings.MasterServerName}...", ConsoleColor.Green);
            TFConsole.WriteLine();
            _ = DeleteExistingDockerContainers();
            TFConsole.WriteLine("Deleting existing Docker containers..., please wait", ConsoleColor.Green);
            TFConsole.WriteLine($"Send POST Data To http://{Settings.MasterServerIp}:{Port}", ConsoleColor.Green);
            TFConsole.WriteLine();
            TFConsole.WriteLine("Waiting for Commands... type 'help' to get a list of commands", ConsoleColor.Green);
            TFConsole.WriteLine();
            TFConsole.WriteLine("Press CTRL+C to exit...", ConsoleColor.Green);

            var gameServers = new List<GameServer>();

            new Thread(() => { ListenForServers(gameServers); }).Start();
            new Thread(() => { ListenForHttpRequestsAsync(gameServers); }).Start();
            new Thread(() => { CheckForEmptyServers(gameServers); }).Start();


            var partySize = 0;
            CreateInitialGameServers(gameServers, null, null, partySize);

            PlayFabAdminAPI.ForgetAllCredentials();
            while (true)
            {
                // Check if the user has entered a command
                var command = Console.ReadLine() ?? "";

                switch (command)
                {
                    case "exit":
                        _isRunning = false;
                        Environment.Exit(0);
                        break;
                    case "help":
                        TFConsole.WriteLine("List of available commands:");
                        TFConsole.WriteLine("add - adds a new game server to the list");
                        TFConsole.WriteLine("remove - removes a game server from the list");
                        TFConsole.WriteLine("list - lists all available game servers");
                        TFConsole.WriteLine("apihelp - lists the API");
                        TFConsole.WriteLine("clear - clears the console");
                        TFConsole.WriteLine("startall - starts all game servers");
                        TFConsole.WriteLine("stopall - stops all game servers");
                        TFConsole.WriteLine("help - displays this list of commands");
                        TFConsole.WriteLine("exit - exits the program");
                        TFConsole.WriteLine("overwrite - overwrites the player numbers settings");
                        break;
                    case "apihelp":
                        TFConsole.WriteLine("API Help");
                        TFConsole.WriteLine(
                            "/connect?partySize=*PartySize* - Connects to a game server with the specified party size eg. /connect?partySize=4");
                        TFConsole.WriteLine("/list-servers - Lists all available game servers");
                        TFConsole.WriteLine("/show-full-servers - Lists all full game servers");
                        TFConsole.WriteLine("/add - Adds a new game server to the list");
                        break;
                    case "clear":
                        Console.Clear();
                        break;
                    case "add":
                        // Parse the arguments for the add command
                        TFConsole.WriteLine("Enter the IP address of the game server:");
                        string addIpAddress = Console.ReadLine() ?? "";
                        TFConsole.WriteLine("Enter the port of the game server:");
                        int addPort = int.Parse(Console.ReadLine() ?? "");

                        CreateGameServers(gameServers, addIpAddress, addPort.ToString(), 0, false);

                        TFConsole.WriteLine(
                            $"Added game server at {addIpAddress}:{addPort} with InstanceID");
                        break;
                    case "remove":
                        // Parse the argument for the remove command
                        TFConsole.WriteLine("Enter the port of the game server:");
                        int removePort = int.Parse(Console.ReadLine() ?? "");
                        // Remove the game server from the list
                        gameServers.RemoveAll(server => server.port == removePort);
                        _ = DeleteDockerContainerByPort(gameServers, removePort);
                        TFConsole.WriteLine($"Removed game server at port {removePort}.");
                        break;
                    case "stopall":
                        _ = StopAllDockerContainers(gameServers);
                        TFConsole.WriteLine("Stopped all game servers.");
                        break;
                    case "startall":
                        _ = StartAllDockerContainers(gameServers);
                        TFConsole.WriteLine("Started all game servers.");
                        break;
                    case "list":
                        // List the available game servers
                        TFConsole.WriteLine("Available game servers:");
                        foreach (GameServer server in gameServers)
                        {
                            TFConsole.WriteLine(
                                $"[{server.instanceId}] {server.ipAddress}:{server.port} ({server.playerCount}/{server.maxCapacity})");
                        }

                        break;
                    case "overwrite":
                        //overwrite player count
                        TFConsole.WriteLine("Enter the port of the game server:");
                        int overwritePort = int.Parse(Console.ReadLine() ?? "");
                        TFConsole.WriteLine("Enter the new player count:");
                        int overwritePlayerCount = int.Parse(Console.ReadLine() ?? "");
                        GameServer? gameServer = gameServers.Find(server => server.port == overwritePort);
                        if (gameServer != null)
                        {
                            gameServer.playerCount = overwritePlayerCount;
                            TFConsole.WriteLine(
                                $"Overwrote player count of game server at port {overwritePort} to {overwritePlayerCount}.");
                        }
                        else
                        {
                            TFConsole.WriteLine($"Game server at port {overwritePort} not found.",ConsoleColor.Red);
                        }
                        break;
                }
            }
        }

        private static async Task DeleteExistingDockerContainers()
        {
            var endpoint = $"{Settings.DockerTcpNetwork}";

            var client = new DockerClientConfiguration(new Uri(endpoint)).CreateClient();

            var containers = client.Containers.ListContainersAsync(new ContainersListParameters()
            {
                All = true
            }).Result;
            try

            {
                foreach (var container in containers)
                {
                    if (container.Names != null)
                    {
                        foreach (var name in container.Names)
                        {
                            if (name.Contains("GameServer"))
                            {
                                client.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters()
                                {
                                    Force = true
                                }).Wait();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                TFConsole.WriteLine($"Error deleting containers: {e.Message}",ConsoleColor.Red);
            }
        }

        private static void CreateInitialGameServers(List<GameServer> gameServers, string ip, string port,
            int partySize)
        {
            if (_isRunning)
            {

                var gameServersToBeCreated = InitialServers.numServers;
                var gameServersCreated = 0;
                var InstancedID = "";
                string serverID;
                if (!Settings.CreateInitialGameServers) return;
                while (true)
                {
                    if (gameServersCreated < gameServersToBeCreated)
                    {
                        CreateDockerContainer(gameServers, ip, port, out InstancedID, out serverID);
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

        private static void CreateGameServers(List<GameServer> gameServers, string ip, string port, int partySize, bool isStandby)
        {
            var gameServersToBeCreated = InitialServers.numServers;
            var InstancedID = String.Empty;
            string serverID;
            

            CreateDockerContainer(gameServers, ip, port, out InstancedID, out serverID);
            CreateNewServer(gameServers, ip, port, InstancedID, serverID, isStandby);
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

        private static GameServer? GetAvailableServer(List<GameServer> gameServers, int partySize)
        {
            // Check if there are any servers in the list
            if (gameServers.Count == 0)
            {
                // If no servers, return null
                return null;
            }

            // Sort the list of game servers by player count
            gameServers.Sort((a, b) => a.playerCount.CompareTo(b.playerCount));

            // Find the first game server with a player count less than its maximum capacity
            var availableServer =
                gameServers.FirstOrDefault(server => server.playerCount + partySize <= server.maxCapacity);
            // Return the available server
            // If no available servers, return the server with the lowest player count
            return availableServer ?? gameServers[0];
        }

        private static void ListenForHttpRequestsAsync(List<GameServer> gameServers)
        {

            var host = Dns.GetHostName();
            var addresses = Dns.GetHostAddresses(host);
            var ipv4Address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            
            var prefixes = new List<string>() {
                
                $"http://127.0.0.1:{WebPort}/",
                $"http://localhost:{WebPort}/",
                $"http://{ipv4Address}:{WebPort}/",
                $"http://127.0.0.1:{WebPort}/"
            };

            HttpListener httpListener = new HttpListener();

            foreach (var prefix in prefixes)
            {
                try
                {
                    httpListener.Prefixes.Add(prefix);
                    httpListener.Start();
                    TFConsole.WriteLine("Successfully started the listener on prefix: " + prefix, ConsoleColor.Green);
                    break;
                }
                catch (HttpListenerException ex)
                {
                    TFConsole.WriteLine("Error adding prefix: " + prefix ,ConsoleColor.Red);
                    TFConsole.WriteLine("Error message: " + ex.Message ,ConsoleColor.Red);
                }
            }

            while (true)
            {
                // Wait for a request to come in
                var context = httpListener.GetContext();

                // Get the request and response objects
                var request = context.Request;
                var response = context.Response;


                // Get the request URL
                
                switch (request.HttpMethod)
                {
                    case "GET":
                        var responseString = string.Empty;
                        switch (request.Url.AbsolutePath.ToLower())
                        {
                            case "/status":
                                responseString = "Server is running";
                                var responseBytes = Encoding.UTF8.GetBytes(responseString);
                                response.ContentLength64 = responseBytes.Length;
                                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                break;
                            case "/admin-panel":
                                // Build the response string for the admin panel
                                responseString = "[";
                                foreach (GameServer server in gameServers)
                                {

                                    string population;

                                    if (server.playerCount >= server.maxCapacity * 0.8) population = "High";
                                    else if (server.playerCount >= server.maxCapacity * 0.5) population = "Medium";
                                    else population = "Low";



                                    if (!server.isActive) continue;
                                    string serverStatus;
                                    if (server.playerCount == server.maxCapacity)
                                        serverStatus = "full";
                                    else
                                        serverStatus = "active";

                                    responseString += "{\"ipAddress\":\"" + server.ipAddress + "\",\"port\":" +
                                                      server.port + ",\"playerCount\":" + server.playerCount +
                                                      ",\"maxCapacity\":" + server.maxCapacity + ",\"status\":\"" +
                                                      serverStatus + "\",\"serverID\":\"" + server.ServerId +
                                                      "\",\"population\":\"" + population + "\"},\n";

                                }

                                responseString = responseString.TrimEnd(',', '\n') + "]";
                                responseBytes = Encoding.UTF8.GetBytes(responseString);
                                response.ContentLength64 = responseBytes.Length;
                                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                break;
                            case "/servers.html":
                                // Get the assembly containing this code
                                Assembly assembly = Assembly.GetExecutingAssembly();
                                // Get the embedded resource stream (also ignore warning, will never be an issue)

#pragma warning disable CS8600
#pragma warning disable CS8604
                                Stream resourceStream =
                                    assembly.GetManifestResourceStream("MasterServer.servers.html");
                                using (StreamReader reader = new StreamReader(resourceStream))
                                {
                                    // Read the contents of the HTML file
                                    var html = reader.ReadToEnd();

                                    // Set the response headers
                                    response.ContentType = "text/html";
                                    response.ContentLength64 = html.Length;


                                    // Write the HTML to the response stream
                                    StreamWriter writer = new StreamWriter(response.OutputStream);
                                    writer.Write(html);
                                    writer.Flush();
                                    writer.Close();
                                    break;
                                }
#pragma warning restore CS8600
#pragma warning restore CS8604
                            // Handle the request
                            case "/list-servers":
                                // Build the response string
                                responseString = "Available game servers:\n";
                                foreach (GameServer server in gameServers)
                                {
                                    responseString += "{\"ipAddress\":\"" + server.ipAddress + "\",\"port\":" +
                                                      server.port + ",\"playerCount\":" + server.playerCount +
                                                      ",\"maxCapacity\":" + server.maxCapacity + "}\n";
                                }

                                responseBytes = Encoding.UTF8.GetBytes(responseString);
                                response.ContentLength64 = responseBytes.Length;
                                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                break;

                            case "/show-full-servers":
                                // Build the response string
                                responseString = "Full game servers:\n";
                                foreach (GameServer server in gameServers)
                                {
                                    if (server.playerCount == server.maxCapacity)
                                    {
                                        responseString = "{\"ipAddress\":\"" + server.ipAddress + "\",\"port\":" +
                                                         server.port + ",\"playerCount\":" + server.playerCount +
                                                         ",\"maxCapacity\":" + server.maxCapacity + "}";
                                    }
                                }

                                responseBytes = Encoding.UTF8.GetBytes(responseString);
                                response.ContentLength64 = responseBytes.Length;
                                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                break;

                            case "/connect":
                                if (Settings.AllowServerJoining)
                                {
                                    responseString = string.Empty;

                                    int partySize;
                                    try
                                    {
                                        var partySizeString = request.QueryString["partySize"] ?? "";
                                        partySize = int.Parse(partySizeString);
                                    }
                                    catch (FormatException)
                                    {
                                        TFConsole.WriteLine("Invalid party size", ConsoleColor.Red);
                                        return;
                                    }

                                    var playfabId = request.QueryString["playfabId"] ?? "";
                                    TFConsole.WriteLine(
                                        $"Request from IP: {request.RemoteEndPoint} with party size: {partySize} {playfabId}");
                                    try
                                    {
                                        ValidateRequest(playfabId);
                                    }
                                    catch (Exception e)
                                    {
                                        TFConsole.WriteLine(e.Message, ConsoleColor.Red);
                                        responseString = e.Message;
                                        responseBytes = Encoding.UTF8.GetBytes(responseString);
                                        response.ContentLength64 = responseBytes.Length;
                                        response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                        break;
                                    }

                                    // Validate token with PlayFab
                                    var isPlayerBanned = ValidateRequest(playfabId);

                                    if (!isPlayerBanned)
                                    {
                                        TFConsole.WriteLine("Player is banned", ConsoleColor.Red);
                                        return;
                                    }

                                    var availableServer = GetAvailableServer((gameServers), partySize);
                                    if (availableServer != null)
                                    {
                                        if (availableServer.playerCount < availableServer.maxCapacity)
                                        {
                                            responseString =
                                                "{\"ipAddress\":\"" + availableServer.ipAddress + "\",\"port\":" +
                                                availableServer.port + ",\"playerCount\":" +
                                                availableServer.playerCount + ",\"maxCapacity\":" +
                                                availableServer.maxCapacity + ",\"playfabId\":\"" +
                                                playfabId + "\"}";
                                            availableServer.playerCount += partySize;

                                            TFConsole.WriteLine(
                                                $"Party of size {partySize} is assigned to : {availableServer.ipAddress}:{availableServer.port} InstanceID:{availableServer.instanceId} Player Count is {availableServer.playerCount}", ConsoleColor.Green);

                                        }
                                        else
                                        {
                                            responseString = "No available game servers";
                                        }

                                    }
                                    else
                                    {
                                        var instancedID = string.Empty;
                                        string serverID;
                                        CreateDockerContainer(gameServers, string.Empty, string.Empty, out instancedID,
                                            out serverID);
                                        GameServer newServer = CreateNewServer(gameServers, string.Empty, string.Empty,
                                            instancedID, serverID,false);
                                        if (newServer != null)
                                        {
                                            responseString =
                                                $"{{\"ipAddress\":\"{newServer.ipAddress}\", \"port\":{newServer.port}, \"playerCount\":{newServer.playerCount}, \"maxCapacity\":{newServer.maxCapacity}, \"InstancedID\":{newServer.instanceId}\"}}";
                                            newServer.playerCount += partySize;
                                        }
                                        else
                                        {
                                            responseString = "Error creating new server";
                                        }
                                    }

                                    responseBytes = Encoding.UTF8.GetBytes(responseString);
                                    response.ContentLength64 = responseBytes.Length;
                                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                }
                                else
                                {
                                    responseString = "Server joining is disabled";
                                    TFConsole.WriteLine("Server joining is disabled", ConsoleColor.Yellow);
                                    responseBytes = Encoding.UTF8.GetBytes(responseString);
                                    response.ContentLength64 = responseBytes.Length;
                                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                }

                                break;
                        }

                        break;

                    case "POST":
                        var requestBody = new StreamReader(request.InputStream).ReadToEnd();
                        var requestLines = requestBody.Split('\n');
                        var requestData = requestLines.Select(line => line.Split('='))
                            .ToDictionary(a => a[0], a => a[1]);

                        // Update the game servers list with the new data
                        gameServers.Add(new GameServer(requestData["ipAddress"], int.Parse(requestData["port"]),
                            int.Parse(requestData["playerCount"]), int.Parse(requestData["maxCapacity"]),
                            requestData["instanceId"], true, requestData["serverId"], false));

                        // Send a response to the server
                        var responseBody = "Received data from game server\n";
                        var responseBodyBytes = Encoding.UTF8.GetBytes(responseBody);
                        response.ContentLength64 = responseBodyBytes.Length;
                        response.OutputStream.Write(responseBodyBytes, 0, responseBodyBytes.Length);
                        break;
                }
            }
        }

        private static bool ValidateRequest(string playfabID)
        {
            if (!Settings.UsePlayFab) return true;
            var adminAPISettings = new PlayFabApiSettings()
            {
                TitleId = Settings.PlayFabTitleID,
                DeveloperSecretKey = Settings.DeveloperSecretKey
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

            if (isBanned > 0)
            {
                return false;
            }

            return true;

        }

        //Server Creation Stuff
        private static GameServer CreateNewServer(List<GameServer> gameServers, string ip, string port,
            string InstancedID, string serverID, bool isStandby)
        {
            var serverIP = DefaultIp;
            var serverPort = _portPool;
            if (!string.IsNullOrEmpty(ip))
            {
                serverIP = ip;
            }

            if (!string.IsNullOrEmpty(port))
            {
                serverPort = Convert.ToInt32(port);
            }

            var gameServer = new GameServer(serverIP, serverPort, 0, Settings.MaxPlayersPerServer, InstancedID, true,
                serverID, isStandby);

            gameServers.Add(gameServer);
            _portPool++;
            return gameServer;
        }

        private static void CreateDockerContainer(List<GameServer> gameServers, string? ip, string? port,
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

            if (!string.IsNullOrEmpty(port))
            {
                HostPort = Convert.ToInt32(port);
            }

            
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
                                Name = $"GameServer-Instance--{_numServers}",
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
        
        private static async Task CheckForEmptyServers(List<GameServer> gameServers)
        {
            while (true)
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
                        _ = DeleteDockerContainer(gameServers, server.instanceId);
                    }
                    else
                    {
                        TFConsole.WriteLine("Standby Server Detected, Not Deleting" ,ConsoleColor.Yellow);
                    }
                }

                gameServers.RemoveAll(server => server.playerCount == 0);
            }
        }

        private static async Task DeleteDockerContainerByPort(List<GameServer> gameServers, int port)
        {
            var endpointUrl = $"{Settings.DockerTcpNetwork}";

            // Create a new DockerClient using the endpoint URL
            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();

            // Get the ID of the container to delete
            var containerId = gameServers.Find(server => server.port == port)?.instanceId;

            // Delete the container
            try
            {
                client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true })
                    .Wait();
            }
            catch (DockerApiException ex)
            {
                TFConsole.WriteLine($"Error deleting container: {ex.Message}",ConsoleColor.Red);
            }
        }

        private static async Task StopAllDockerContainers(List<GameServer> gameServers)
        {
            var endpointUrl = $"{Settings.DockerTcpNetwork}";

            // Create a new DockerClient using the endpoint URL
            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();

            var containers = client.Containers.ListContainersAsync(new ContainersListParameters()
            {
                All = true
            }).Result;

            try
            {
                foreach (var container in containers)
                {
                    if (container.Names != null)
                    {
                        if (container.Names[0].Contains("GameServer-Instance--"))
                        {
                            client.Containers.StopContainerAsync(container.ID, new ContainerStopParameters()
                            {
                                WaitBeforeKillSeconds = 10
                            }).Wait();
                        }
                    }
                }
            }
            catch (DockerApiException ex)
            {
                TFConsole.WriteLine($"Error stopping container: {ex.Message}",ConsoleColor.Red);
            }
        }

        private static async Task StartAllDockerContainers(List<GameServer> gameServers)
        {
            var endpointUrl = $"{Settings.DockerTcpNetwork}";

            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();

            IList<ContainerListResponse> containers = client.Containers.ListContainersAsync(
                new ContainersListParameters()
                {
                    All = true
                }).Result;

            try
            {
                foreach (var container in containers)
                {
                    if (container.Names != null)
                    {
                        if (container.Names[0].Contains("GameServer-Instance--"))
                        {
                            client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters()
                            {
                            }).Wait();
                        }
                    }
                }
            }
            catch (DockerApiException ex)
            {
                TFConsole.WriteLine($"Error stopping container: {ex.Message}",ConsoleColor.Red);
            }
        }

        private static Task DeleteDockerContainer(List<GameServer> gameServers, string containerId)
        {
            // Set the API endpoint URL
            var endpointUrl = $"{Settings.DockerTcpNetwork}";

            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();


            if (Settings.AllowServerDeletion)
            {
                // Delete the container by its ID
                try
                {
                    client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters()).Wait();
                }
                catch (DockerApiException ex)
                {
                    TFConsole.WriteLine($"Error deleting container: {ex.Message}",ConsoleColor.Red);
                }
            }
            else
            {
                try
                {
                    client.Containers.StopContainerAsync(containerId, new ContainerStopParameters()).Wait();
                }
                catch (DockerApiException ex)
                {
                    TFConsole.WriteLine($"Error stopping container: {ex.Message}",ConsoleColor.Red);
                }
            }

            return Task.CompletedTask;
        }

        private static async void ListenForServers(List<GameServer> gameServers)
        {
            // Create a TCP listener
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            while (true)
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

                if (gameServer == null)
                {
                    TFConsole.WriteLine($"Received data from unknown game server: {serverID}",ConsoleColor.Red);
                    continue;
                }

                // Update the game server's player count
                gameServer.playerCount = playerCount;

                TFConsole.WriteLine($"Received data from game server {serverID}: {playerCount} players",ConsoleColor.Green);

                // Close the connection with the game server
                client.Close();
            }
        }
    }
}
