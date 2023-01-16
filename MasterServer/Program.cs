﻿using System.Diagnostics;
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
        private static string _networkString = "127.0.0.1"; //Change this to your network ip if you want to run the server on a different machine (If Docker is running then this is changed to the Bridge Network)
        private static bool _isRunning = true;

        /// <summary>
        /// Stops The Main Loop When False
        /// </summary>
        private static bool MainThreadRunning { get; set; } = true;
        
        private static Thread ListenForServersThread { get; set; }
        private static readonly CancellationTokenSource  ListenForServersCancellationToken = new CancellationTokenSource ();
        private static Thread ListenForHttpRequestsThread { get; set; }
        private static readonly CancellationTokenSource  ListenForHttpRequestsCancellationToken  = new CancellationTokenSource ();
        private static Thread CheckForEmptyServersThread { get; set; }
        private static readonly CancellationTokenSource  CheckForEmptyServersCancellationToken = new CancellationTokenSource ();

        public static readonly CommandService CommandService = new CommandService();
        private static readonly List<GameServer> Servers = new List<GameServer>();
        
        public static List<GameServer> GetServers() => Servers;
        public static GameServer? GetServer(int port)
        {
            return Servers.FirstOrDefault(server => server.port == port);
        }
        public static void RemoveServer(int port)
        {
            GameServer? gameServer = GetServer(port);
            if (gameServer != null)
            {
                Servers.Remove(gameServer);
            }
        }
        
        public static async Task Main(string[] args)
        {
            Startup();
            
            ListenForServersThread = new Thread(() => { ListenForServers(Servers, ListenForServersCancellationToken.Token); });
            ListenForHttpRequestsThread = new Thread(() => { ListenForHttpRequestsAsync(Servers,ListenForHttpRequestsCancellationToken.Token); });
            CheckForEmptyServersThread = new Thread(() => { CheckForEmptyServers(Servers, CheckForEmptyServersCancellationToken.Token); });
            
            ListenForServersThread.Start();
            ListenForHttpRequestsThread.Start();
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
                _ = DeleteExistingDockerContainers();
            }
            TFConsole.WriteLine("Deleting existing Docker containers..., please wait", ConsoleColor.Green);
            TFConsole.WriteLine($"Send POST Data To http://{Settings.MasterServerIp}:{Port}\n", ConsoleColor.Green);
            TFConsole.WriteLine("Waiting for Commands... type 'help' to get a list of commands\n", ConsoleColor.Green);
            TFConsole.WriteLine("Type Quit or Exit to Close Application.", ConsoleColor.Green);
        }

        public static void Quit()
        {
            // Stop Main Thread Loop
            MainThreadRunning = false;
            _isRunning = false;
            
            // Stop Running Threads
            ListenForServersCancellationToken.Cancel();
            ListenForHttpRequestsCancellationToken.Cancel();
            CheckForEmptyServersCancellationToken.Cancel();
            ListenForServersThread.Join();
            ListenForHttpRequestsThread.Join();
            CheckForEmptyServersThread.Join();
            StopAllDockerContainers();
            
            // Save Current Settings To File
            Settings.SaveToDisk();
            
            Environment.Exit(0);
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

        private static void CreateInitialGameServers(List<GameServer> gameServers, string ip, int? port,
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

        public static void CreateGameServers(string ip, int port, int partySize, bool isStandby)
        {
            var gameServersToBeCreated = InitialServers.numServers;
            var InstancedID = String.Empty;
            string serverID;
            

            CreateDockerContainer(Servers, ip, port, out InstancedID, out serverID);
            CreateNewServer(Servers, ip, port, InstancedID, serverID, isStandby);
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

        private static void ListenForHttpRequestsAsync(List<GameServer> gameServers,
            CancellationToken cancellationToken)
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

            while (!cancellationToken.IsCancellationRequested)
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
                                        CreateDockerContainer(gameServers, string.Empty, null, out instancedID,
                                            out serverID);
                                        GameServer newServer = CreateNewServer(gameServers, string.Empty, null,
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
        private static GameServer CreateNewServer(List<GameServer> gameServers, string ip, int? port,
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

            gameServers.Add(gameServer);
            _portPool++;
            return gameServer;
        }

        private static void CreateDockerContainer(List<GameServer> gameServers, string? ip, int? port,
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

        public static async Task DeleteDockerContainerByPort(int port)
        {
            var endpointUrl = $"{Settings.DockerTcpNetwork}";

            // Create a new DockerClient using the endpoint URL
            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();

            // Get the ID of the container to delete
            var containerId = Servers.Find(server => server.port == port)?.instanceId;

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

        public static async Task StopAllDockerContainers()
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

        public static async Task StartAllDockerContainers()
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
