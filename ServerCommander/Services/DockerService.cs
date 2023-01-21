﻿using Docker.DotNet;
using Docker.DotNet.Models;
using ServerCommander.Settings.Config;

namespace ServerCommander.Services;

public class DockerService
{
    private readonly MasterServerSettings _settings;

    public DockerService(MasterServerSettings settings)
    {
        _settings = settings;
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
                        await client.Containers.StopContainerAsync(container.ID, new ContainerStopParameters()
                        {
                            WaitBeforeKillSeconds = 10
                        });
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
                        await client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters());
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
                            await client.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters()
                            {
                                Force = true
                            });
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

        public async Task DeleteDockerContainerByPort(string containerId)
        {
            var endpointUrl = $"{_settings.DockerTcpNetwork}";

            // Create a new DockerClient using the endpoint URL
            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();
            
            // Delete the container
            try
            {
                await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
            }
            catch (DockerApiException ex)
            {
                TFConsole.WriteLine($"Error deleting container: {ex.Message}",ConsoleColor.Red);
            }
        }


        public async Task DeleteDockerContainer(string containerId)
        {
            // Set the API endpoint URL
            var endpointUrl = $"{_settings.DockerTcpNetwork}";

            var client = new DockerClientConfiguration(new Uri(endpointUrl)).CreateClient();


            if (_settings.AllowServerDeletion)
            {
                // Delete the container by its ID
                try
                {
                    await client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters());
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
                    await client.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
                }
                catch (DockerApiException ex)
                {
                    TFConsole.WriteLine($"Error stopping container: {ex.Message}",ConsoleColor.Red);
                }
            }
        }
}