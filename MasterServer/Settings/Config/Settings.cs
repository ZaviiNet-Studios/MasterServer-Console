﻿namespace MasterServer
{
    public class Settings
    {
        public Settings(bool createInitialGameServers = default, bool createStandbyGameServers = default, string dockerContainerImage = null, string dockerHost = null, string dockerNetwork = null,
            string dockerTcpNetwork = null, bool dockerContainerAutoRemove = default,
            bool dockerContainerAutoStart = default, bool dockerContainerAutoUpdate = default,
            string masterServerIp = null, int masterServerWebPort = default, int masterServerApiPort = default,
            int masterServerPort = default, string masterServerName = null, string masterServerPassword = null,
            int maxGameServers = default, int maxPlayers = default, int maxPartyMembers = default, int maxPlayersPerServer = default,
            bool allowServerCreation = default, bool allowServerDeletion = default, bool allowServerJoining = default,
            bool serverRestartOnCrash = default, bool serverRestartOnShutdown = default,
            bool serverRestartOnUpdate = default, bool serverRestartSchedule = default,
            string serverRestartScheduleTime = null, int gameServerPortPool = default,
            bool gameServerRandomPorts = default)
        {
            CreateInitialGameServers = createInitialGameServers;
            CreateStandbyGameServers = createStandbyGameServers;
            DockerContainerImage = dockerContainerImage;
            DockerHost = dockerHost;
            DockerNetwork = dockerNetwork;
            DockerTcpNetwork = dockerTcpNetwork;
            DockerContainerAutoRemove = dockerContainerAutoRemove;
            DockerContainerAutoStart = dockerContainerAutoStart;
            DockerContainerAutoUpdate = dockerContainerAutoUpdate;
            MasterServerIp = masterServerIp;
            MasterServerWebPort = masterServerWebPort;
            MasterServerApiPort = masterServerApiPort;
            MasterServerPort = masterServerPort;
            MasterServerName = masterServerName;
            MasterServerPassword = masterServerPassword;
            MaxGameServers = maxGameServers;
            MaxPlayers = maxPlayers;
            MaxPartyMembers = maxPartyMembers;
            MaxPlayersPerServer = maxPlayersPerServer;
            AllowServerCreation = allowServerCreation;
            AllowServerDeletion = allowServerDeletion;
            AllowServerJoining = allowServerJoining;
            ServerRestartOnCrash = serverRestartOnCrash;
            ServerRestartOnShutdown = serverRestartOnShutdown;
            ServerRestartOnUpdate = serverRestartOnUpdate;
            ServerRestartSchedule = serverRestartSchedule;
            ServerRestartScheduleTime = serverRestartScheduleTime;
            GameServerPortPool = gameServerPortPool;
            GameServerRandomPorts = gameServerRandomPorts;
        }

        public string DockerContainerImage { get; set; }
        public string DockerHost { get; set; } = "unix:///var/run/docker.sock";
        public string DockerNetwork { get; set; }
        public string DockerTcpNetwork { get; set; } = "tcp://localhost:2375";
        public bool DockerContainerAutoRemove { get; set; }
        public bool DockerContainerAutoStart { get; set; }
        public bool DockerContainerAutoUpdate { get; set; }

        public string MasterServerIp { get; set; }
        public int MasterServerWebPort { get; set; }
        public int MasterServerApiPort { get; set; }
        public int MasterServerPort { get; set; }

        public string MasterServerName { get; set; }
        public string MasterServerPassword { get; set; }

        public int MaxGameServers { get; set; }
        public int MaxPlayers { get; set; }
        public int MaxPartyMembers { get; set; }
        public int MaxPlayersPerServer { get; set; } 
        public bool AllowServerCreation { get; set; }
        public bool AllowServerDeletion { get; set; }
        public bool AllowServerJoining { get; set; }
        public bool ServerRestartOnCrash { get; set; }
        public bool ServerRestartOnShutdown { get; set; }
        public bool ServerRestartOnUpdate { get; set; }
        public bool ServerRestartSchedule { get; set; }
        public string ServerRestartScheduleTime { get; set; } 
        public int GameServerPortPool { get; set; }
        public bool GameServerRandomPorts { get; set; }
        
        public bool CreateInitialGameServers { get; set; }
        public bool CreateStandbyGameServers { get; set; }
    }
}