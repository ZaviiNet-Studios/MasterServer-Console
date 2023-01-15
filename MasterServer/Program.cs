using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.AdminModels;
using PlayFab.AuthenticationModels;
using PlayFab.Internal;

namespace MasterServer
{
    class Program
    {
        private static int _numServers;
        private static readonly Settings Settings = LoadSettings();
        private static readonly GameServers InitialServers = InitialDockerContainerSettings();
        private static readonly int Port = Settings.MasterServerPort;
        private static readonly int WebPort = Settings.MasterServerApiPort;
        private static readonly string DefaultIp = Settings.MasterServerIp;
        private static int _portPool = Settings.GameServerPortPool;

        
        public static void Main(string[] args)
        {

            TFConsole.Start();

            TFConsole.WriteLine("Loading MasterServer-Console");
            TFConsole.WriteLine();
            TFConsole.WriteLine($"Starting {Settings.MasterServerName}...");
            TFConsole.WriteLine();
            DeleteExistingDockerContainers();
            TFConsole.WriteLine("Deleting existing Docker containers...");
            TFConsole.WriteLine($"Send POST Data To http://{Settings.MasterServerIp}:{Port}");
            TFConsole.WriteLine();
            TFConsole.WriteLine("Waiting for Commands... type 'help' to get a list of commands");
            TFConsole.WriteLine();
            TFConsole.WriteLine("Press CTRL+C to exit...");

            List<GameServer> gameServers = new System.Collections.Generic.List<GameServer>();

            System.Threading.Thread listenThread = new System.Threading.Thread(() => ListenForServers(gameServers));
            listenThread.Start();
            Thread httpListenThread =
                new Thread(() => ListenForHttpRequestsAsync(gameServers));
            httpListenThread.Start();
            

            var partySize = 0;
            CreateInitialGameServers(gameServers,null,null,partySize);
            
            PlayFabAdminAPI.ForgetAllCredentials();
            while (true)
            {
                // Check if the user has entered a command
                var command = Console.ReadLine();

                switch (command)
                {
                    case "help":
                        TFConsole.WriteLine("List of available commands:");
                        TFConsole.WriteLine("add - adds a new game server to the list");
                        TFConsole.WriteLine("remove - removes a game server from the list");
                        TFConsole.WriteLine("list - lists all available game servers");
                        TFConsole.WriteLine("apihelp - lists the API");
                        TFConsole.WriteLine("clear - clears the console");
                        TFConsole.WriteLine("startall - starts all game servers");
                        TFConsole.WriteLine("stopall - stops all game servers");
                        //TFConsole.WriteLine("connect - connects to a game server with the specified party size");
                        TFConsole.WriteLine("help - displays this list of commands");
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
                        var addIpAddress = Console.ReadLine();
                        TFConsole.WriteLine("Enter the port of the game server:");
                        var addPort = int.Parse(Console.ReadLine());
                        TFConsole.WriteLine("Enter the current number of players on the game server:");
                        var addPlayerCount = int.Parse(Console.ReadLine());
                        TFConsole.WriteLine("Enter the maximum capacity of the game server:");
                        var addMaxCapacity = int.Parse(Console.ReadLine());
                        TFConsole.WriteLine("Enter the instanceID of the game server:");
                        var addInstanceId = Console.ReadLine();

                        // Add the game server to the list
                        gameServers.Add(new GameServer(addIpAddress, addPort, addPlayerCount, addMaxCapacity,
                            addInstanceId,true));
                        TFConsole.WriteLine(
                            $"Added game server at {addIpAddress}:{addPort} with InstanceID {addInstanceId}.");
                        break;
                    case "remove":
                        // Parse the argument for the remove command
                        TFConsole.WriteLine("Enter the port of the game server:");
                        var removePort = int.Parse(Console.ReadLine());
                        // Remove the game server from the list
                        gameServers.RemoveAll(server => server.port == removePort);
                        DeleteDockerContainerByPort(gameServers, removePort);
                        TFConsole.WriteLine($"Removed game server at port {removePort}.");
                        break;
                    case "stopall":
                        StopAllDockerContainers(gameServers);
                        TFConsole.WriteLine("Stopped all game servers.");
                        break;
                    case "startall":
                        StartAllDockerContainers(gameServers);
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
                        var overwritePort = int.Parse(Console.ReadLine());
                        TFConsole.WriteLine("Enter the new player count:");
                        var overwritePlayerCount = int.Parse(Console.ReadLine());
                        GameServer gameServer = gameServers.Find(server => server.port == overwritePort);
                        if (gameServer != null)
                        {
                            gameServer.playerCount = overwritePlayerCount;
                            TFConsole.WriteLine($"Overwrote player count of game server at port {overwritePort} to {overwritePlayerCount}.");
                        }
                        else
                        {
                            TFConsole.WriteLine($"Game server at port {overwritePort} not found.");
                        }
                        break;
                }
            }
        }
        
        private static void DeleteExistingDockerContainers()
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
                TFConsole.WriteLine($"Error deleting containers: {e.Message}");
            }
        }

        private static void CreateInitialGameServers(List<GameServer> gameServers,string ip, string port, int partySize)
        {
            var gameServersToBeCreated = InitialServers.numServers;
            var gameServersCreated = 0;
            var InstancedID = "";
            if (Settings.CreateInitialGameServers)
            {
                while (true)
                {
                    if (gameServersCreated < gameServersToBeCreated)
                    {
                        CreateDockerContainer(gameServers,ip,port,partySize, out InstancedID);
                        CreateNewServer(gameServers,ip,port, partySize, InstancedID);
                        InstancedID = string.Empty;
                        gameServersCreated++;
                    }
                    else
                    {
                        TFConsole.WriteLine($"Initial game servers created successfully - Number Created = {gameServersCreated}");
                        break;
                    }
                }
            }
        }

        private static GameServers InitialDockerContainerSettings()
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
                GameServers initialSettings = JsonConvert.DeserializeObject<GameServers>(json);
                return initialSettings;
            }
        }

        private static Settings LoadSettings()
        {
            var filePath = "config/settings.json";

            if (!File.Exists(filePath))
            {
                Settings defaultSettings = new Settings
                {
                    CreateInitialGameServers = true,
                    CreateStandbyGameServers = false,
                    DockerContainerImage = "alpine",
                    DockerContainerImageTag = "latest",
                    DockerHost = "unix:///var/run/docker.sock",
                    DockerNetwork = "bridge",
                    DockerTcpNetwork = "tcp://localhost:2376",
                    DockerContainerAutoRemove = true,
                    DockerContainerAutoStart = true,
                    DockerContainerAutoUpdate = true,
                    MasterServerIp = "localhost",
                    MasterServerWebPort = 80,
                    MasterServerApiPort = 8080,
                    MasterServerPort = 13000,
                    MasterServerName = "Master Server Instance",
                    MasterServerPassword = "password",
                    MaxGameServers = 100,
                    MaxPlayers = 10000,
                    MaxPlayersPerServer = 50,
                    MaxPartyMembers = 5,
                    AllowServerCreation = true,
                    AllowServerDeletion = true,
                    AllowServerJoining = true,
                    ServerRestartOnCrash = true,
                    ServerRestartOnShutdown = false,
                    ServerRestartOnUpdate = false,
                    ServerRestartSchedule = true,
                    ServerRestartScheduleTime = "00:00",
                    GameServerPortPool = 5100,
                    GameServerRandomPorts = false
                };
                var json = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);

                Directory.CreateDirectory("config");
                File.WriteAllText(filePath, json);
                return defaultSettings;
            }
            else
            {
                string json = File.ReadAllText(filePath);
                Settings settings = JsonConvert.DeserializeObject<Settings>(json);
                return settings;
            }
        }

        private static GameServer GetAvailableServer(List<GameServer> gameServers, int partySize)
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
            GameServer availableServer =
                gameServers.FirstOrDefault(server => server.playerCount + partySize <= server.maxCapacity);
            // Return the available server
            // If no available servers, return the server with the lowest player count
            return availableServer ?? gameServers[0];
        }

        private static async Task ListenForHttpRequestsAsync(List<GameServer> gameServers)
        {
            // Create a new HTTP listener
            HttpListener httpListener = new HttpListener();

            // Add the prefixes to the listener
            httpListener.Prefixes.Add($"http://*:{WebPort}/");
            
            

            // Start the listener
            httpListener.Start();

            while (true)
            {
                // Wait for a request to come in
                HttpListenerContext context = httpListener.GetContext();

                // Get the request and response objects
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;


                // Get the request URL

                //string requestUrl = request.Url.AbsolutePath.ToLower();

                switch (request.HttpMethod)
                {
                    case "GET":
                        var responseString = string.Empty;
                        switch (request.Url.AbsolutePath)
                        {
                            case "/admin-panel":
                            {
                                // Build the response string for the admin panel
                                responseString = "[";
                                foreach (GameServer server in gameServers)
                                {
                                    if (server.isActive)
                                    {
                                        string serverStatus = (server.playerCount == server.maxCapacity) ? "full" : "active";
                                        responseString += "{\"ipAddress\":\"" + server.ipAddress + "\",\"port\":" +
                                                          server.port + ",\"playerCount\":" + server.playerCount +
                                                          ",\"maxCapacity\":" + server.maxCapacity + ",\"status\":\"" + serverStatus + "\"},\n";
                                    }
                                }
                                responseString = responseString.TrimEnd(',', '\n') + "]";
                                var responseBytes = Encoding.UTF8.GetBytes(responseString);
                                response.ContentLength64 = responseBytes.Length;
                                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                break;
                            }
                            case "/servers.html":
                            {
                                // Get the assembly containing this code
                                Assembly assembly = Assembly.GetExecutingAssembly();
                                // Get the embedded resource stream
                                Stream resourceStream =
                                    assembly.GetManifestResourceStream("MasterServer.servers.html");
                                using (StreamReader reader = new StreamReader(resourceStream))
                                {
                                    // Read the contents of the HTML file
                                    string html = reader.ReadToEnd();

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
                            }
                            // Handle the request
                            case "/list-servers":
                            {
                                // Build the response string
                                responseString = "Available game servers:\n";
                                foreach (GameServer server in gameServers)
                                {
                                    responseString += "{\"ipAddress\":\"" + server.ipAddress + "\",\"port\":" +
                                                      server.port + ",\"playerCount\":" + server.playerCount +
                                                      ",\"maxCapacity\":" + server.maxCapacity + "}\n";
                                }
                                var responseBytes = Encoding.UTF8.GetBytes(responseString);
                                response.ContentLength64 = responseBytes.Length;
                                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                break;
                            }
                            case "/show-full-servers":
                            {
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
                                var responseBytes = Encoding.UTF8.GetBytes(responseString);
                                response.ContentLength64 = responseBytes.Length;
                                response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                break;
                            }
                            case "/connect":
                            {
                                if (Settings.AllowServerJoining)
                                {
                                    responseString = string.Empty;
                                    var partySizeString = request.QueryString["partySize"];
                                    var partySize = int.Parse(partySizeString);
                                    var playfabId = request.QueryString["playfabId"];
                                    TFConsole.WriteLine($"Request from IP: {request.RemoteEndPoint} with party size: {partySize} {playfabId}");

                                    ValidateRequest(playfabId);
                                    
                                    // Validate token with PlayFab
                                    bool isPlayerBanned = ValidateRequest(playfabId);

                                    if (!isPlayerBanned)
                                    {
                                        TFConsole.WriteLine("Player is banned");
                                        responseString = "{\"isBanned\":true}";
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

                                            TFConsole.WriteLine($"Party of size {partySize} is assigned to : {availableServer.ipAddress}:{availableServer.port} InstanceID:{availableServer.instanceId} Player Count is {availableServer.playerCount}");
                                            
                                        }
                                        else
                                        {
                                            responseString = "No available game servers";
                                        }
                                        
                                    }
                                    else
                                    {
                                        var instancedID = string.Empty;
                                        CreateDockerContainer(gameServers, null,null,partySize, out instancedID);
                                        GameServer newServer = CreateNewServer(gameServers, null,null,partySize, instancedID);
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
                                    var responseBytes = Encoding.UTF8.GetBytes(responseString);
                                    response.ContentLength64 = responseBytes.Length;
                                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                }
                                else
                                {
                                    responseString = "Server joining is disabled";
                                    TFConsole.WriteLine("Server joining is disabled");
                                    var responseBytes = Encoding.UTF8.GetBytes(responseString);
                                    response.ContentLength64 = responseBytes.Length;
                                    response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                                }
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
                            requestData["instanceId"], true) );

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

            var task = authenticationApi.GetUserBansAsync(request);
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
        

        private static GameServer CreateNewServer(List<GameServer> gameServers, string ip,string port, int partySize, string InstancedID)
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


            var gameServer = new GameServer(serverIP, serverPort, 0, Settings.MaxPlayersPerServer, InstancedID, true);
            gameServers.Add(gameServer);
            _portPool++;
            return gameServer;
        }

        private static void CreateDockerContainer(List<GameServer> gameServers,string ip, string port, int partySize, out string InstancedID)
        {
            var newInstancedID = "";
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
            
            TFConsole.WriteLine($"New Server Requested with IP {HostIP} and Port {HostPort}");

            if (Settings.AllowServerCreation)
            {
                if (gameServers.Count < Settings.MaxGameServers)
                {
                    string endpointUrl = $"{Settings.DockerTcpNetwork}";

                    // Create a new DockerClient using the endpoint URL
                    DockerClient client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();

                    // Set the image name and tag to use for the new container
                    string imageName = $"{Settings.DockerContainerImage}";
                    string imageTag = $"{Settings.DockerContainerImageTag}";
                    
                    // Create a new container using the image name and tag, and the specified command
                    var createResponse = client.Containers.CreateContainerAsync(new CreateContainerParameters
                    {
                        Image = imageName + ":" + imageTag,
                        Name = $"GameServer-Instance--{_numServers}",
                        Hostname = HostIP,
                        ExposedPorts = new Dictionary<string, EmptyStruct>
                        {
                            {"7777/udp", default(EmptyStruct)}
                        },
                        HostConfig = new HostConfig
                        {
                            PortBindings = new Dictionary<string, IList<PortBinding>>
                            {
                                { "7777/udp", new List<PortBinding> { new PortBinding { HostPort = HostPort+"/udp" } } },
                            }
                        }
                    }).Result;

                    // Get the ID of the new container
                    string containerId = createResponse.ID;
                    newInstancedID = containerId;

                    // Start the new container
                    client.Containers.StartContainerAsync(containerId, null).Wait();
                    _numServers++;
                    
                    TFConsole.WriteLine($"New Server Created with ID {newInstancedID}");
                }
                else
                {
                    TFConsole.WriteLine("Max game servers reached");
                }
            }
            else
            {
                TFConsole.WriteLine("Server creation is disabled");
            }

            InstancedID = newInstancedID;
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
                    if (server.playerCount == 0)
                    {
                        DeleteDockerContainer(gameServers, server.instanceId);
                    }
                }
                gameServers.RemoveAll(server => server.playerCount == 0);
            }
        }

        private static async Task DeleteDockerContainerByPort(List<GameServer> gameServers, int port)
        {
            string endpointUrl = $"{Settings.DockerTcpNetwork}";
            
            // Create a new DockerClient using the endpoint URL
            DockerClient client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();
            
            // Get the ID of the container to delete
            string containerId = gameServers.Find(server => server.port == port).instanceId;
            
            // Delete the container
            try
            {
                client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true })
                    .Wait();
            }
            catch (DockerApiException ex)
            {
                TFConsole.WriteLine($"Error deleting container: {ex.Message}");
            }
        }

        private static async Task StopAllDockerContainers(List<GameServer> gameServers)
        {
            string endpointUrl = $"{Settings.DockerTcpNetwork}";
            
            // Create a new DockerClient using the endpoint URL
            DockerClient client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();
            
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
                TFConsole.WriteLine($"Error stopping container: {ex.Message}");
            }
            
        }

        private static async Task StartAllDockerContainers(List<GameServer> gameServers)
        {
            string endpointUrl = $"{Settings.DockerTcpNetwork}";
            
            DockerClient client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();
            
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
                            client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters()
                            {
                            }).Wait();
                        }
                    }
                }
            }
            catch (DockerApiException ex)
            {
                TFConsole.WriteLine($"Error stopping container: {ex.Message}");
            }
        }

        private static async Task DeleteDockerContainer(List<GameServer> gameServers, string containerId)
        {
            // Set the API endpoint URL
            string endpointUrl = $"{Settings.DockerTcpNetwork}";

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
                    TFConsole.WriteLine($"Error deleting container: {ex.Message}");
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
                    TFConsole.WriteLine($"Error stopping container: {ex.Message}");
                }
            }
        }

        private static async Task ListenForServers(List<GameServer> gameServers)
        {
            // Create a TCP listener
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            while (true)
            {
                // Wait for a game server to connect
                var client = listener.AcceptTcpClient();

                // Read the data sent by the game server
                NetworkStream stream = client.GetStream();
                var data = new byte[1024];
                var bytesRead = stream.Read(data, 0, data.Length);
                var dataString = System.Text.Encoding.ASCII.GetString(data, 0, bytesRead);

                // Check for and remove any headers

                if (dataString.Contains("Content-Length:"))
                {
                    int contentLength =
                        int.Parse(dataString.Substring(
                            dataString.IndexOf("Content-Length:") + 16, 3));
                    dataString = dataString.Substring(dataString.IndexOf("\r\n\r\n") + 4,
                        contentLength);
                }


                // Split the data into separate values
                Dictionary<string, string> values = dataString.Split('&')
                    .Select(s => s.Split('='))
                    .ToDictionary(a => a[0], a => a[1]);
                if (!values.ContainsKey("ipAddress") || !values.ContainsKey("port") ||
                    !values.ContainsKey("playerCount") || !values.ContainsKey("maxCapacity"))
                {
                    TFConsole.WriteLine($"Received invalid data from game server: {dataString} (Expected format: ipAddress=127.0.0.1&port=7777&playerCount=0&maxCapacity=50)");
                    continue;
                }

                var ipAddress = values["ipAddress"];
                var port = int.Parse(values["port"]);
                var playerCount = int.Parse(values["playerCount"]);
                var maxCapacity = int.Parse(values["maxCapacity"]);
                var instanceId = "";

                // Check if the game server is already in the list
                GameServer gameServer = gameServers.Find(server => server.ipAddress == ipAddress && server.port == port);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (gameServer == null)
                {
                    if (gameServers.Count < Settings.MaxGameServers)
                    {
                        CreateDockerContainer(gameServers, ipAddress,port.ToString(),0, out instanceId);
                        CreateNewServer(gameServers,ipAddress,port.ToString(), 0, instanceId);
                    }
                    else
                    {
                        TFConsole.WriteLine("Maximum number of game servers reached");
                    }

                }
                else
                {
                    // If in the list, update its player count
                    gameServer.playerCount = playerCount;
                }

                // Close the connection with the game server
                client.Close();
            }
        }
    }
}
               
