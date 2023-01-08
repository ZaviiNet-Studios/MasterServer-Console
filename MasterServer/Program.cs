using System.Net;
using System.Net.Sockets;

namespace MasterServer
{
    class Program
    {
        static readonly int port = 13000;

        static void Main(string[] args)
        {
            Console.WriteLine("MasterServer");
            Console.WriteLine();
            Console.WriteLine("Starting MasterServer...");
            Console.WriteLine();
            Console.WriteLine($"Send POST Data To http://localhost:{port}");
            Console.WriteLine();
            
            List<GameServer> gameServers = new List<GameServer>();
            
            Thread listenThread = new Thread(() => ListenForServers(gameServers));
            listenThread.Start();

            while (true)
            {
                // Check if the user has entered a command
                var command = Console.ReadLine();

                switch (command)
                {
                    case "add":
                        // Parse the arguments for the add command
                        Console.WriteLine("Enter the IP address of the game server:");
                        var addIpAddress = Console.ReadLine();
                        Console.WriteLine("Enter the port of the game server:");
                        var addPort = int.Parse(Console.ReadLine()!);
                        Console.WriteLine("Enter the current number of players on the game server:");
                        var addPlayerCount = int.Parse(Console.ReadLine()!);
                        Console.WriteLine("Enter the maximum capacity of the game server:");
                        var addMaxCapacity = int.Parse(Console.ReadLine()!);
                        Console.WriteLine("Enter the instanceID of the game server:");
                        var addInstanceId = Console.ReadLine();

                        // Add the game server to the list
                        gameServers.Add(new GameServer(addIpAddress!, addPort, addPlayerCount, addMaxCapacity, addInstanceId!));
                        Console.WriteLine($"Added game server at {addIpAddress}:{addPort} with InstanceID {addInstanceId}.");
                        break;
                    
                    case "list":
                        // List the available game servers
                        Console.WriteLine("Available game servers:");
                        foreach (GameServer server in gameServers)
                        {
                            Console.WriteLine(
                                $"[{server.instanceId}] {server.ipAddress}:{server.port} ({server.playerCount}/{server.maxCapacity})");
                        }

                        break;

                    case "connect":
                        // Find an available game server and connect to it
                        GameServer availableServer = GetAvailableServer((gameServers));
                        if (availableServer.playerCount < availableServer.maxCapacity)
                        {
                            Console.WriteLine($"Sever has {availableServer.playerCount} players out of {availableServer.maxCapacity}");
                            Console.WriteLine($"Connecting to {availableServer.ipAddress}:{availableServer.port}");
                            availableServer.playerCount++;
                        }
                        else
                        {
                            Console.WriteLine("No available game servers");
                        }

                        break;

                }
            }
        }

        static GameServer GetAvailableServer(List<GameServer> gameServers)
        {
            // Check if there are any servers in the list
            if (gameServers.Count == 0)
            {
                // If no servers, return null
                return null!;
            }

            // Sort the list of game servers by player count
            gameServers.Sort((a, b) => a.playerCount.CompareTo(b.playerCount));

            // Find the first game server with a player count less than its maximum capacity
            GameServer availableServer = gameServers.FirstOrDefault(server => server.playerCount < server.maxCapacity);
            // Return the available server
            // If no available servers, return the server with the lowest player count
            return availableServer ?? gameServers[0];
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
                    int contentLength = int.Parse(dataString.Substring(dataString.IndexOf("Content-Length:") + 16, 3));
                    dataString = dataString.Substring(dataString.IndexOf("\r\n\r\n") + 4, contentLength);
                }


                // Split the data into separate values
                Dictionary<string, string> values = dataString.Split('&')
                    .Select(s => s.Split('='))
                    .ToDictionary(a => a[0], a => a[1]);
                if (!values.ContainsKey("ipAddress") || !values.ContainsKey("port") ||
                    !values.ContainsKey("playerCount") || !values.ContainsKey("maxCapacity") || !values.ContainsKey("instanceId"))
                {
                    Console.WriteLine($"Received invalid data from game server: {dataString} (Expected format: ipAddress=127.0.0.1&port=7777&playerCount=0&maxCapacity=50&instanceId=1234567890)");
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
                    gameServers.Add(new GameServer(ipAddress, port, playerCount, maxCapacity, instanceId));
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
               
