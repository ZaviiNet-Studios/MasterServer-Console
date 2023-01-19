using Newtonsoft.Json;

namespace ServerCommander.Settings.Config
{
    public class MasterServerSettings
    {
        public const string DefaultFilePath = "config/settings.json";
        
        public MasterServerSettings(bool createInitialGameServers = default, int numberofInitialGameServers = default, bool createStandbyGameServers = default, string? dockerContainerImage = null, string? dockerContainerImageTag = null, string? dockerHost = null, string? dockerNetwork = null,
            string? dockerTcpNetwork = null, bool dockerContainerAutoRemove = default,
            bool dockerContainerAutoStart = default, bool dockerContainerAutoUpdate = default,
            string? masterServerIp = null, int masterServerWebPort = default, int masterServerApiPort = default,
            int masterServerPort = default, string? masterServerName = null, string? masterServerPassword = null,
            int maxGameServers = default, int maxPlayers = default, int maxPartyMembers = default, int maxPlayersPerServer = default,
            bool allowServerCreation = default, bool allowServerDeletion = default, bool allowServerJoining = default,
            bool serverRestartOnCrash = default, bool serverRestartOnShutdown = default,
            bool serverRestartOnUpdate = default, bool serverRestartSchedule = default,
            string? serverRestartScheduleTime = null, int gameServerPortPool = default,
            bool gameServerRandomPorts = default,bool usePlayFab = default ,string? playFabTitleID = null, string? developerSecretKey = null)
        {
            CreateInitialGameServers = createInitialGameServers;
            NumberofInitialGameServers = numberofInitialGameServers;
            CreateStandbyGameServers = createStandbyGameServers;
            DockerContainerImage = dockerContainerImage;
            DockerContainerImageTag = dockerContainerImageTag;
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
            UsePlayFab = usePlayFab;
            PlayFabTitleID = playFabTitleID;
            DeveloperSecretKey = developerSecretKey;
        }
        
        public static MasterServerSettings Default => new MasterServerSettings
        {
            CreateInitialGameServers = true,
            CreateStandbyGameServers = false,
            DockerContainerImage = "alpine",
            DockerContainerImageTag = "latest",
            DockerHost = "unix:///var/run/docker.sock",
            DockerNetwork = "bridge",
            DockerTcpNetwork = "tcp://localhost:2375",
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
            GameServerRandomPorts = false,
            UsePlayFab = false,
            PlayFabTitleID = null,
            DeveloperSecretKey = null,
        };
        
        public static MasterServerSettings GetFromDisk(string filePath = DefaultFilePath)
        {
            if (!File.Exists(filePath))
                return Default;
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<MasterServerSettings>(json) ?? Default;
        }
        
        public void SaveToDisk(string filePath = DefaultFilePath)
        {
            // Create the directory if it doesn't exist
            string directory = Path.GetDirectoryName(filePath) ?? throw new Exception("Failed to Get Directory");
            if(!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        
        public static void SaveDefaultToDisk(string filePath = DefaultFilePath)
        {
            Default.SaveToDisk(filePath);
        }

        public string? DockerContainerImage { get; set; }
        
        public string? DockerContainerImageTag { get; set; }
        public string? DockerHost { get; set; } 
        public string? DockerNetwork { get; set; }
        public string? DockerTcpNetwork { get; set; }
        public bool DockerContainerAutoRemove { get; set; }
        public bool DockerContainerAutoStart { get; set; }
        public bool DockerContainerAutoUpdate { get; set; }
        
        public int NumberofInitialGameServers { get; set; }

        public string? MasterServerIp { get; set; }
        public int MasterServerWebPort { get; set; }
        public int MasterServerApiPort { get; set; }
        public int MasterServerPort { get; set; }

        public string? MasterServerName { get; set; }
        public string? MasterServerPassword { get; set; }

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
        public string? ServerRestartScheduleTime { get; set; } 
        public int GameServerPortPool { get; set; }
        public bool GameServerRandomPorts { get; set; }
        
        public bool CreateInitialGameServers { get; set; }
        public bool CreateStandbyGameServers { get; set; }
        
        public bool UsePlayFab { get; set; }
        public string? PlayFabTitleID { get; set; }
        public string? DeveloperSecretKey { get; set; }
    }
}