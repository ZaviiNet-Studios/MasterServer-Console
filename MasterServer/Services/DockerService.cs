using Docker.DotNet;
using Docker.DotNet.Models;
using ServerCommander.Classes;
using ServerCommander.Settings.Config;

namespace ServerCommander.Services;

public class DockerService
{
    private readonly MasterServerSettings _settings;
    private static readonly DatabaseService _databaseService;

    public DockerService(MasterServerSettings settings)
    {
        _settings = settings;
    }
    
    public async Task CheckDatabaseAgainstRunningServers(List<GameServer> gameServers)
    {
            var endpointUrl = $"{_settings.DockerTcpNetwork}";

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
                        Program.CreateInitialGameServers(_databaseService.Servers, null, null, 0);
                        return;
                    }
                }
                TFConsole.WriteLine("No Docker Containers Found", ConsoleColor.Red);
                gameServers.Clear();
                _ = _databaseService.RemoveAllGameServers();
                Program.CreateInitialGameServers(_databaseService.Servers, null, null, 0);
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
                        if (_settings.AllowServerDeletion)
                        {
                            TFConsole.WriteLine($"Deleting Game Server {instanceId}", ConsoleColor.Green);
                            _ = DeleteDockerContainerByID(instanceId);
                        }
                    }
                    else
                    {
                        var containerPort = gameServer.port;
                        TFConsole.WriteLine(
                            $"Found Container: {container.Names.First()} with ID: {instanceId} and port {containerPort}, Recreating Server",
                            ConsoleColor.Green);
                        _ = DeleteDockerContainerByID(instanceId);
                        _ = _databaseService.RemoveAllGameServers();
                        gameServers.Remove(gameServer);
                        Program.ReCreateServer(_databaseService.Servers, container.Names.First(), null, containerPort, 0);
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
                _ = DeleteExistingDockerContainers();
                Program.CreateInitialGameServers(_databaseService.Servers, null, null, 0);
            }
        }

    public async Task StopAllDockerContainers()
    {
        var endpointUrl = $"{_settings.DockerTcpNetwork}";

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
            TFConsole.WriteLine($"Error stopping container: {ex.Message}", ConsoleColor.Red);
        }
    }

    public async Task StartAllDockerContainers()
    {
        var endpointUrl = $"{_settings.DockerTcpNetwork}";

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
            TFConsole.WriteLine($"Error stopping container: {ex.Message}", ConsoleColor.Red);
        }
    }

    public async Task DeleteExistingDockerContainers()
    {
        var endpoint = $"{_settings.DockerTcpNetwork}";

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
            TFConsole.WriteLine($"Error deleting containers: {e.Message}", ConsoleColor.Red);
        }
    }

        public async Task DeleteDockerContainerByPort(int port)
        {
            var endpointUrl = $"{_settings.DockerTcpNetwork}";

            // Create a new DockerClient using the endpoint URL
            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();

            // Get the ID of the container to delete
            var containerId = Program.GetServer(port)?.instanceId;

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
        
        public async Task DeleteDockerContainerByID(string id)
        {
            var endpointUrl = $"{_settings.DockerTcpNetwork}";

            // Create a new DockerClient using the endpoint URL
            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();

            // Get the ID of the container to delete
            var containerId = id;

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


        public Task DeleteDockerContainer(string containerId)
        {
            // Set the API endpoint URL
            var endpointUrl = $"{_settings.DockerTcpNetwork}";

            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();


            if (_settings.AllowServerDeletion)
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
}