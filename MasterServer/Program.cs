using System.Net;
using System.Net.Sockets;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using Newtonsoft.Json;

namespace MasterServer
{
    class Program
    {
        private static int numServers = 0;
        static Settings settings = LoadSettings();
        private static GameServers InitialServers = InitialDockerContainerSettings();
        static readonly int port = settings.MasterServerPort;
        private static readonly int webPort = settings.MasterServerApiPort;
        private static readonly string defaultIP = settings.MasterServerIp;
        private static int portPool = settings.GameServerPortPool;

        static void Main(string[] args)
        {

            System.Console.WriteLine("Loading MasterServer-Console");
            System.Console.WriteLine();
            System.Console.WriteLine($"Starting {settings.MasterServerName}...");
            System.Console.WriteLine();
            DeleteExistingDockerContainers();
            System.Console.WriteLine("Deleting existing Docker containers...");
            System.Console.WriteLine($"Send POST Data To http://{settings.MasterServerIp}:{port}");
            System.Console.WriteLine();
            System.Console.WriteLine("Waiting for Commands... type 'help' to get a list of commands");
            System.Console.WriteLine();
            System.Console.WriteLine("Press CTRL+C to exit...");

            System.Collections.Generic.List<GameServer> gameServers = new System.Collections.Generic.List<GameServer>();

            System.Threading.Thread listenThread = new System.Threading.Thread(() => ListenForServers(gameServers));
            listenThread.Start();
            System.Threading.Thread httpListenThread =
                new System.Threading.Thread(() => ListenForHttpRequests(gameServers));
            httpListenThread.Start();
            System.Threading.Thread checkServersThread =
                new System.Threading.Thread(() => CheckForEmptyServers(gameServers));
            checkServersThread.Start();
            var partySize = 0;
            CreateInitialGameServers(gameServers, partySize);

            while (true)
            {
                // Check if the user has entered a command
                var command = Console.ReadLine();

                switch (command)
                {
                    case "help":
                        Console.WriteLine("List of available commands:");
                        Console.WriteLine("add - adds a new game server to the list");
                        Console.WriteLine("remove - removes a game server from the list");
                        Console.WriteLine("list - lists all available game servers");
                        Console.WriteLine("apihelp - lists the API");
                        Console.WriteLine("clear - clears the console");
                        Console.WriteLine("startall - starts all game servers");
                        Console.WriteLine("stopall - stops all game servers");
                        //Console.WriteLine("connect - connects to a game server with the specified party size");
                        Console.WriteLine("help - displays this list of commands");
                        break;
                    case "apihelp":
                        Console.WriteLine("API Help");
                        Console.WriteLine(
                            "/connect?partySize=*PartySize* - Connects to a game server with the specified party size eg. /connect?partySize=4");
                        Console.WriteLine("/list-servers - Lists all available game servers");
                        Console.WriteLine("/show-full-servers - Lists all full game servers");
                        Console.WriteLine("/add - Adds a new game server to the list");
                        break;
                    case "clear":
                        Console.Clear();
                        break;
                    case "add":
                        // Parse the arguments for the add command
                        System.Console.WriteLine("Enter the IP address of the game server:");
                        var addIpAddress = Console.ReadLine();
                        System.Console.WriteLine("Enter the port of the game server:");
                        var addPort = int.Parse(Console.ReadLine());
                        System.Console.WriteLine("Enter the current number of players on the game server:");
                        var addPlayerCount = int.Parse(Console.ReadLine());
                        System.Console.WriteLine("Enter the maximum capacity of the game server:");
                        var addMaxCapacity = int.Parse(Console.ReadLine());
                        System.Console.WriteLine("Enter the instanceID of the game server:");
                        var addInstanceId = Console.ReadLine();

                        // Add the game server to the list
                        gameServers.Add(new GameServer(addIpAddress, addPort, addPlayerCount, addMaxCapacity,
                            addInstanceId));
                        System.Console.WriteLine(
                            $"Added game server at {addIpAddress}:{addPort} with InstanceID {addInstanceId}.");
                        break;
                    case "remove":
                        // Parse the argument for the remove command
                        Console.WriteLine("Enter the port of the game server:");
                        var removePort = int.Parse(Console.ReadLine());
                        // Remove the game server from the list
                        gameServers.RemoveAll(server => server.port == removePort);
                        DeleteDockerContainerByPort(gameServers, removePort);
                        Console.WriteLine($"Removed game server at port {removePort}.");
                        break;
                    case "stopall":
                        StopAllDockerContainers(gameServers);
                        Console.WriteLine("Stopped all game servers.");
                        break;
                    case "startall":
                        StartAllDockerContainers(gameServers);
                        Console.WriteLine("Started all game servers.");
                        break;
                    case "list":
                        // List the available game servers
                        System.Console.WriteLine("Available game servers:");
                        foreach (GameServer server in gameServers)
                        {
                            System.Console.WriteLine(
                                $"[{server.instanceId}] {server.ipAddress}:{server.port} ({server.playerCount}/{server.maxCapacity})");
                        }

                        break;

                    // case "connect":
                    //     System.Console.WriteLine("Enter party size:");
                    //     string partySizeString = Console.ReadLine();
                    //     int partySize = int.Parse(partySizeString);
                    //     // Find an available game server and connect to it
                    //     GameServer availableServer = GetAvailableServer((gameServers), partySize);
                    //     if (availableServer.playerCount < availableServer.maxCapacity)
                    //     {
                    //         System.Console.WriteLine(
                    //             $"Sever has {availableServer.playerCount} players out of {availableServer.maxCapacity}");
                    //         System.Console.WriteLine(
                    //             $"Connecting to {availableServer.ipAddress}:{availableServer.port}");
                    //         availableServer.playerCount++;
                    //     }
                    //     else
                    //     {
                    //         System.Console.WriteLine("No available game servers");
                    //     }
                    //     break;
                }
            }
        }

        private static void DeleteExistingDockerContainers()
        {
            string endpoint = $"{settings.DockerTcpNetwork}";

            DockerClient client = new DockerClientConfiguration(new Uri(endpoint)).CreateClient();
            
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
                Console.WriteLine("Error deleting containers: {0}", e.Message);
            }
        }

        private static void CreateInitialGameServers(List<GameServer> gameServers, int partySize)
        {
            var gameServersToBeCreated = InitialServers.numServers;
            var gameServersCreated = 0;
            var InstancedID = "";
            if (settings.CreateInitialGameServers)
            {
                while (true)
                {
                    if (gameServersCreated < gameServersToBeCreated)
                    {
                        CreateDockerContainer(gameServers, partySize, out InstancedID);
                        CreateNewServer(gameServers, partySize, InstancedID);
                        InstancedID = string.Empty;
                        gameServersCreated++;
                    }
                    else
                    {
                        Console.WriteLine("Initial game servers created successfully - Number Created = {0}", gameServersCreated);
                        break;
                    }
                }
            }
        }

        private static GameServers InitialDockerContainerSettings()
        {
            string filepath = "config/initialGameServers.json";
            if (!File.Exists(filepath))
            {
                GameServers initialSettings = new GameServers
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
                GameServers initialSettings = JsonConvert.DeserializeObject<GameServers>(json);
                return initialSettings;
            }
        }

        private static Settings LoadSettings()
        {
            string filePath = "config/settings.json";

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
                string json = JsonConvert.SerializeObject(defaultSettings, Formatting.Indented);

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

        static GameServer GetAvailableServer(List<GameServer> gameServers, int partySize)
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

        private static void SendResponse(TcpClient client, string response)
        {
            // Convert the response string to a byte array
            var responseBytes = System.Text.Encoding.ASCII.GetBytes(response);

            // Send the response to the client
            var stream = client.GetStream();
            stream.Write(responseBytes, 0, responseBytes.Length);

            // Close the connection with the client
            client.Close();
        }

        static void ListenForHttpRequests(List<GameServer> gameServers)
        {
            // Create a new HTTP listener
            HttpListener httpListener = new HttpListener();

            // Add the prefixes to the listener
            httpListener.Prefixes.Add("http://localhost:8080/");

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
                        var responseString = "";
                        switch (request.Url.AbsolutePath)
                        {
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

                                break;
                            }
                            case "/connect":
                            {
                                if (settings.AllowServerJoining)
                                {
                                    responseString = "";
                                    var partySizeString = request.QueryString["partySize"];
                                    var partySize = int.Parse(partySizeString);
                                    var availableServer = GetAvailableServer((gameServers), partySize);
                                    if (availableServer != null)
                                    {
                                        if (availableServer.playerCount < availableServer.maxCapacity)
                                        {
                                            responseString =
                                                "{\"ipAddress\":\"" + availableServer.ipAddress + "\",\"port\":" +
                                                availableServer.port + ",\"playerCount\":" +
                                                availableServer.playerCount + ",\"maxCapacity\":" +
                                                availableServer.maxCapacity + "}";
                                            availableServer.playerCount += partySize;
                                        }
                                        else
                                        {
                                            responseString = "No available game servers";
                                        }
                                    }
                                    else
                                    {
                                        var instancedID = "";
                                        CreateDockerContainer(gameServers, partySize, out instancedID);
                                        GameServer newServer = CreateNewServer(gameServers, partySize, instancedID);
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
                                }
                                else
                                {
                                    responseString = "Server joining is disabled";
                                    Console.WriteLine("Server joining is disabled");
                                }
                            }
                                break;
                        }

                        var responseBytes = Encoding.UTF8.GetBytes(responseString);
                        response.ContentLength64 = responseBytes.Length;
                        response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                        break;

                    case "POST":
                        var requestBody = new StreamReader(request.InputStream).ReadToEnd();
                        var requestLines = requestBody.Split('\n');
                        var requestData = requestLines.Select(line => line.Split('='))
                            .ToDictionary(a => a[0], a => a[1]);

                        // Update the game servers list with the new data
                        gameServers.Add(new GameServer(requestData["ipAddress"], int.Parse(requestData["port"]),
                            int.Parse(requestData["playerCount"]), int.Parse(requestData["maxCapacity"]),
                            requestData["instanceId"]));

                        // Send a response to the server
                        var responseBody = "Received data from game server\n";
                        var responseBodyBytes = Encoding.UTF8.GetBytes(responseBody);
                        response.ContentLength64 = responseBodyBytes.Length;
                        response.OutputStream.Write(responseBodyBytes, 0, responseBodyBytes.Length);
                        break;
                }
            }
        }


        private static GameServer CreateNewServer(List<GameServer> gameServers, int partySize, string InstancedID)
        {
            var gameServer = new GameServer(defaultIP, portPool, 0, settings.MaxPlayersPerServer, InstancedID);
            gameServers.Add(gameServer);
            portPool++;
            return gameServer;
        }

        private static void CreateDockerContainer(List<GameServer> gameServers, int partySize, out string InstancedID)
        {
            var newInstancedID = "";
            if (settings.AllowServerCreation)
            {
                if (gameServers.Count < settings.MaxGameServers)
                {
                    string endpointUrl = $"{settings.DockerTcpNetwork}";

                    // Create a new DockerClient using the endpoint URL
                    DockerClient client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();

                    // Set the image name and tag to use for the new container
                    string imageName = $"{settings.DockerContainerImage}";
                    string imageTag = $"{settings.DockerContainerImageTag}";
                    
                    // Create a new container using the image name and tag, and the specified command
                    var createResponse = client.Containers.CreateContainerAsync(new CreateContainerParameters
                    {
                        Image = imageName + ":" + imageTag,
                        Name = $"GameServer-Instance--{numServers}",
                        Hostname = "0.0.0.0",
                        ExposedPorts = new Dictionary<string, EmptyStruct>
                        {
                            {"7777/udp", default(EmptyStruct)}
                        },
                        HostConfig = new HostConfig
                        {
                            PortBindings = new Dictionary<string, IList<PortBinding>>
                            {
                                { "7777/udp", new List<PortBinding> { new PortBinding { HostPort = portPool+"/udp" } } },
                            }
                        }
                    }).Result;

                    // Get the ID of the new container
                    string containerId = createResponse.ID;
                    newInstancedID = containerId;

                    // Start the new container
                    client.Containers.StartContainerAsync(containerId, null).Wait();
                    numServers++;
                }
                else
                {
                    Console.WriteLine("Max game servers reached");
                }
            }
            else
            {
                Console.WriteLine("Server creation is disabled");
            }

            InstancedID = newInstancedID;
        }

        static void CheckForEmptyServers(List<GameServer> gameServers)
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

        static void DeleteDockerContainerByPort(List<GameServer> gameServers, int port)
        {
            string endpointUrl = $"{settings.DockerTcpNetwork}";
            
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
                Console.WriteLine("Error deleting container: {0}", ex.Message);
            }
        }

        static void StopAllDockerContainers(List<GameServer> gameServers)
        {
            string endpointUrl = $"{settings.DockerTcpNetwork}";
            
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
                Console.WriteLine("Error stopping container: {0}", ex.Message);
            }
        }

        static void StartAllDockerContainers(List<GameServer> gameServers)
        {
            string endpointUrl = $"{settings.DockerTcpNetwork}";
            
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
                Console.WriteLine("Error stopping container: {0}", ex.Message);
            }
            
        }

        static void DeleteDockerContainer(List<GameServer> gameServers, string containerId)
        {
            // Set the API endpoint URL
            string endpointUrl = $"{settings.DockerTcpNetwork}";

            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();


            if (settings.AllowServerDeletion)
            {
                // Delete the container by its ID
                try
                {
                    client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters()).Wait();
                }
                catch (DockerApiException ex)
                {
                    Console.WriteLine("Error deleting container: {0}", ex.Message);
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
                    Console.WriteLine("Error stopping container: {0}", ex.Message);
                }
            }
        }

        private static void ListenForServers(List<GameServer> gameServers)
        {
            // Create a TCP listener
            var listener = new TcpListener(IPAddress.Any, port);
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
                    !values.ContainsKey("playerCount") || !values.ContainsKey("maxCapacity") ||
                    !values.ContainsKey("instanceId"))
                {
                    System.Console.WriteLine(
                        $"Received invalid data from game server: {dataString} (Expected format: ipAddress=127.0.0.1&port=7777&playerCount=0&maxCapacity=50&instanceId=1234567890)");
                    continue;
                }

                var ipAddress = values["ipAddress"];
                var port = int.Parse(values["port"]);
                var playerCount = int.Parse(values["playerCount"]);
                var maxCapacity = int.Parse(values["maxCapacity"]);
                var instanceId = values["instanceId"];

                // Check if the game server is already in the list
                GameServer gameServer = gameServers.Find(server => server.ipAddress == ipAddress && server.port == port);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (gameServer == null)
                {
                    // If not in the list, add it
                    gameServers.Add(new GameServer(ipAddress, port, playerCount, maxCapacity,
                        instanceId));
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
               
