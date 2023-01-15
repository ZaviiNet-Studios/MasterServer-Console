
# Master Server Console Application

This console application allows you to manage a list of game servers and perform various actions on them, such as adding or deleting servers from the list, and creating or deleting Docker containers.

This is Non-Production use, Alot of Functions are still Work In Progress and are not ready.


## Requirements

- .NET Core 3.1
- Docker with linux contains & tcp daemon enabled.
- A Linux GameServer Docker Image with port 7777 Exposed - See [DockerFile](https://github.com/ZaviiNet-Studios/MasterServer-Console/blob/master/Dockerfile) for example
## Configuration Variables

To run this project, you will need to add the following environment variables to your settings.json file located in config

`DockerContainerImage`: The name of the Docker image to use for creating new game servers.

`DockerHost`: The hostname or IP address of the Docker daemon.

`DockerNetwork`: The network to attach the new container to.

`DockerTcpNetwork`: The network endpoint to connect to the Docker daemon.

`DockerContainerAutoRemove`: Automatically remove the container when it stops.

`DockerContainerAutoStart`: Automatically start the container when the daemon starts.

`DockerContainerAutoUpdate`: Automatically update the container when a newer version is available.

`MasterServerIp`: The IP address of the master server.

`MasterServerWebPort`: The port for the master server's web interface.

`MasterServerApiPort`: The port for the master server's API.

`MasterServerPort`: The port for the master server.

`MasterServerName`: The name of the master server.

`MasterServerPassword`: The password for the master server.

`MaxGameServers`: The maximum number of game servers that can be created.

`MaxPlayers`: The maximum number of players that can be connected to the master server.

`MaxPartyMembers`: The maximum number of party members allowed per player.

`MaxPlayersPerServer`: The maximum number of players allowed per game server.

`AllowServerCreation`: Allow the creation of new game servers.

`AllowServerDeletion`: Allow the deletion of game servers.

`AllowServerJoining`: Allow players to connect to game servers.

`ServerRestartOnCrash`: Automatically restart game servers if they crash.

`ServerRestartOnShutdown`: Automatically restart game servers if they shut down.

`ServerRestartOnUpdate`: Automatically restart game servers if they are updated.

`ServerRestartSchedule`: Enable a scheduled restart for game servers.

`ServerRestartScheduleTime`: The time to schedule the restart of game servers.

`GameServerPortPool`: The pool of ports to use for game servers.

`GameServerRandomPorts`: Use random ports for game servers instead of sequential ones.

`CreateInitialGameServers`: Create game servers when the application starts.

`CreateStandbyGameServers`: Create game servers in standby to handle incoming player connections.
# Usage

To use the console application, navigate to the directory where the executable is located and run it from the command line or just open the exe. The application will wait for you to enter a command. Available commands include:


- `add`: Add a new game server to the list. The application will prompt you to enter the IP address, port, current number of players, maximum capacity, and instance ID of the game server.

- `remove`: Remove a game server from the list by specifying its port number.

- `list`: List all game servers in the current list.

- `connect`: Attempt to connect to a game server that has available capacity for the specified party size. The application will return the IP address and port of the game server, if one is found.

- `create`: Create a new Docker container with the specified party size. The application will use the settings specified in the settings.json file to determine the image and ports to use for the container.

- `delete`: Delete a Docker container by specifying its ID.

- `startall` : Starts all Existing Servers.

- `stopall` : Stops all Servers.

- `help`: Display a list of available commands.

settings: Edit the settings for the console application. The settings are stored in the settings.json file and include options for the Docker image and ports to use, as well as various other options for managing game servers and containers.
## API Commands

Default way is to send commands to http://localhost:13000 or check the console app for instruction

`/connect?partySize=2` This will reply back a server that can host a partySize of 2, or Create a server if not

`/list-servers` This will give you a json list of servers

`/show-full-servers` This will show you full servers

You can also POST Commands with just sending the raw data

`ipAddress=192.168.1.57&port=7778&playerCount=0&maxCapacity=50&instanceId=123`



## Notes

The settings.json file is created the first time the application is run, and contains default values for the various settings. You can edit this file to customize the behavior of the application.

The gameServers.json file stores the list of game servers. This file is created the first time the add command is used, and is updated whenever the list of game servers is modified.

The console application uses the Docker API to manage Docker containers. Make sure that the Docker daemon is running on your machine before using the create and delete commands.

The console application uses the .NET HTTPClient library to make API requests. Ensure that you have the necessary dependencies installed.

## Authors

- [@ZaviiNet](https://github.com/ZaviiNet)

## Contributors

- [@verlox](https://github.com/verlox)




## Contributing

Contributions are always welcome!

Open an Issue to get started, Pull Requests welcome!

Please adhere to this project's `code of conduct`.



## License

[gpl-3.0](https://choosealicense.com/licenses/gpl-3.0/)

