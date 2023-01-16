using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using PlayFab;
using PlayFab.AdminModels;
using ServerCommander.Classes;
using ServerCommander.Settings.Config;

namespace ServerCommander.Services;

public class HttpService
{
    private readonly int _port;
    private readonly MasterServerSettings _settings;

    private static Thread thread { get; set; }
    private static readonly CancellationTokenSource cancellationToken = new();

    public HttpService(int port, MasterServerSettings settings)
    {
        _port = port;
        _settings = settings;
    }

    public void Stop()
    {
        cancellationToken.Cancel();
    }

    public void Start(List<GameServer> gameServers)
    {
        thread = new Thread(() => { ListenForHttpRequests(gameServers); });

        thread.Start();
    }

    private void ListenForHttpRequests(List<GameServer> gameServers)
    {

        var host = Dns.GetHostName();
        var addresses = Dns.GetHostAddresses(host);
        var ipv4Address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

        var prefixes = new List<string>()
        {

            $"http://127.0.0.1:{_port}/",
            $"http://localhost:{_port}/",
            $"http://{ipv4Address}:{_port}/",
            $"http://127.0.0.1:{_port}/"
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
                TFConsole.WriteLine("Error adding prefix: " + prefix, ConsoleColor.Red);
                TFConsole.WriteLine("Error message: " + ex.Message, ConsoleColor.Red);
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
                    switch (request.Url.AbsolutePath.ToLower())
                    {
                        case "/status":
                            var responseBytes = Encoding.UTF8.GetBytes("Server is running");
                            response.ContentLength64 = responseBytes.Length;
                            response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                            break;
                        case "/admin-panel":
                            AdminPanelEndpoint(response, gameServers);
                            break;
                        case "/servers.html":
                            ServerHtmlEndpoint(request, response);
                            break;
                        case "/list-servers":
                            ListServerEndpoint(response, gameServers);
                            break;
                        case "/show-full-servers":
                            ListFullServersEndpoint(response, gameServers);
                            break;
                        case "/connect":
                            ConnectEndpoint(request, response, gameServers);
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

    private void AdminPanelEndpoint(HttpListenerResponse response, List<GameServer> gameServers)
    {
        var responseString = JsonSerializer.Serialize(gameServers.Select(x => new
        {
            x.ipAddress,
            x.port,
            x.playerCount,
            x.maxCapacity,
            status = x.GetStatus(),
            population = x.GetPopulation()
        }));
        var responseBytes = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = responseBytes.Length;
        response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
    }

    void ConnectEndpoint(HttpListenerRequest request, HttpListenerResponse response,
        List<GameServer> gameServers)
    {
        var responseString = string.Empty;
        if (_settings.AllowServerJoining)
        {

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
                return;
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
                        $"Party of size {partySize} is assigned to : {availableServer.ipAddress}:{availableServer.port} InstanceID:{availableServer.instanceId} Player Count is {availableServer.playerCount}",
                        ConsoleColor.Green);

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
                Program.CreateDockerContainer(gameServers, string.Empty, null, out instancedID,
                    out serverID);
                GameServer newServer = Program.CreateNewServer(gameServers, string.Empty, null,
                    instancedID, serverID, false);
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
            TFConsole.WriteLine("Server joining is disabled", ConsoleColor.Yellow);
        }
    }

    static GameServer? GetAvailableServer(List<GameServer> gameServers, int partySize)
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


    bool ValidateRequest(string playfabID)
    {
        if (!_settings.UsePlayFab) return true;
        var adminAPISettings = new PlayFabApiSettings()
        {
            TitleId = _settings.PlayFabTitleID,
            DeveloperSecretKey = _settings.DeveloperSecretKey
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

    private void ServerHtmlEndpoint(HttpListenerRequest request, HttpListenerResponse response)
    {
        // Get the assembly containing this code
        Assembly assembly = Assembly.GetExecutingAssembly();
        // Get the embedded resource stream (also ignore warning, will never be an issue
        Stream resourceStream =
            assembly.GetManifestResourceStream("MasterServer.servers.html");
        using StreamReader reader = new StreamReader(resourceStream);
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
    }

    private void ListFullServersEndpoint(HttpListenerResponse response, List<GameServer> gameServers)
    {
        // Build the response string
        var responseString = JsonSerializer.Serialize(gameServers.Where(s => s.playerCount == s.maxCapacity)
            .Select(x => new
            {
                x.ipAddress,
                x.port,
                x.playerCount,
                x.maxCapacity
            }));
        var responseBytes = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = responseBytes.Length;
        response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
    }

    private void ListServerEndpoint(HttpListenerResponse response, List<GameServer> gameServers)
    {
        // Build the response string
        var responseString = JsonSerializer.Serialize(gameServers.Select(x => new
        {
            x.ipAddress,
            x.port,
            x.playerCount,
            x.maxCapacity
        }));
        var responseBytes = Encoding.UTF8.GetBytes(responseString);
        response.ContentLength64 = responseBytes.Length;
        response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
    }
}