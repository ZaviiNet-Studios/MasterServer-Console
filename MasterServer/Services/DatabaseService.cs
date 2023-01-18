using System.Data.SQLite;
using ServerCommander.Classes;
using ServerCommander.Settings.Config;

namespace ServerCommander.Services;

public class DatabaseService
{
    private readonly MasterServerSettings _settings;

    public List<GameServer> Servers;

    public DatabaseService(MasterServerSettings settings)
    {
        _settings = settings;
    }

    public async Task CreateDatabaseAndTable()
    {
        try
        {
            if (!File.Exists($"{_settings.DatabaseName}"))
            {
                SQLiteConnection.CreateFile($"{_settings.DatabaseName}");
                TFConsole.WriteLine($"Database created: {_settings.DatabaseName}", ConsoleColor.Green);
            }

            await using (var connection = new SQLiteConnection($"Data Source={_settings.DatabaseName};Version=3;"))
            {
                connection.Open();

                var command = new SQLiteCommand(connection);
                command.CommandText = @"CREATE TABLE IF NOT EXISTS GameServer (
                                    ipAddress TEXT NOT NULL,
                                    port INTEGER NOT NULL,
                                    playerCount INTEGER NOT NULL,
                                    maxCapacity INTEGER NOT NULL,
                                    instanceId TEXT NOT NULL,
                                    isActive INTEGER NOT NULL,
                                    serverId TEXT NOT NULL,
                                    isStandby INTEGER NOT NULL
                                )";

                TFConsole.WriteLine("Creating table GameServer", ConsoleColor.Green);

                command.ExecuteNonQuery();

                connection.Close();
                _settings.DatabaseCreated = true;
                _settings.SaveToDisk();
            }
        }
        catch (Exception e)
        {
            TFConsole.WriteLine("Error creating database and table", ConsoleColor.Red);
            TFConsole.WriteLine(e.Message, ConsoleColor.Red);
        }
    }

    public async Task<List<GameServer>> LoadGameServersAsync()
    {
        Servers = new List<GameServer>();

        using (var connection = new SQLiteConnection($"Data Source={_settings.DatabaseName};Version=3;"))
        {
            connection.Open();

            var command = new SQLiteCommand("SELECT * FROM GameServer", connection);
            var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var ipAddress = reader["ipAddress"].ToString();
                var port = int.Parse(reader["port"].ToString());
                var playerCount = int.Parse(reader["playerCount"].ToString());
                var maxCapacity = int.Parse(reader["maxCapacity"].ToString());
                var instanceId = reader["instanceId"].ToString();
                var isActive = reader["isActive"].ToString() == "1" ? true : false;
                var serverId = reader["serverId"].ToString();
                var isStandby = reader["isStandby"].ToString() == "1" ? true : false;

                var gameServer = new GameServer(ipAddress, port, playerCount, maxCapacity, instanceId, isActive,
                    serverId, isStandby);
                Servers.Add(gameServer);
            }

            reader.Close();
            connection.Close();
        }

        foreach (var gameServer in Servers)
        {
            TFConsole.WriteLine($"Loaded game server {gameServer.instanceId} from database", ConsoleColor.Green);
            _settings.GameServerPortPool++;
        }
        

        return Servers;
    }

    public async Task SaveGameServer(string IpAddress, int Port, int PlayerCount, int MaxCapacity,
        string InstanceId, bool IsActive, string ServerId, bool IsStandby)
    {
        using (var connection = new SQLiteConnection($"Data Source={_settings.DatabaseName};Version=3;"))
        {
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO GameServer (ipAddress, port, playerCount, maxCapacity, instanceId, isActive, serverId, isStandby) VALUES (@ipAddress, @port, @playerCount, @maxCapacity, @instanceId, @isActive, @serverId, @isStandby)";
                command.Parameters.AddWithValue("@ipAddress", IpAddress);
                command.Parameters.AddWithValue("@port", Port);
                command.Parameters.AddWithValue("@playerCount", PlayerCount);
                command.Parameters.AddWithValue("@maxCapacity", MaxCapacity);
                command.Parameters.AddWithValue("@instanceId", InstanceId);
                command.Parameters.AddWithValue("@isActive", IsActive);
                command.Parameters.AddWithValue("@serverId", ServerId);
                command.Parameters.AddWithValue("@isStandby", IsStandby);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task UpdateGameServer(string IpAddress, int Port, int PlayerCount, int MaxCapacity,
        string InstanceId, bool IsActive, string ServerId, bool IsStandby)
    {
        using (var connection = new SQLiteConnection($"Data Source={_settings.DatabaseName};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE GameServer SET ipAddress = @ipAddress, port = @port, playerCount = @playerCount, maxCapacity = @maxCapacity, instanceId = @instanceId, isActive = @isActive, serverId = @serverId, isStandby = @isStandby WHERE ipAddress = @ipAddress AND port = @port";
                command.Parameters.AddWithValue("@ipAddress", IpAddress);
                command.Parameters.AddWithValue("@port", Port);
                command.Parameters.AddWithValue("@playerCount", PlayerCount);
                command.Parameters.AddWithValue("@maxCapacity", MaxCapacity);
                command.Parameters.AddWithValue("@instanceId", InstanceId);
                command.Parameters.AddWithValue("@isActive", IsActive);
                command.Parameters.AddWithValue("@serverId", ServerId);
                command.Parameters.AddWithValue("@isStandby", IsStandby);
                await command.ExecuteNonQueryAsync();
            }
        }
    }


    public async Task UpdateGameServerPlayerNumbers(int playercount, string ServerId)
    {
        using (var connection = new SQLiteConnection($"Data Source={_settings.DatabaseName};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE GameServer SET playerCount = @playercount WHERE serverId = @ServerId";
                command.Parameters.AddWithValue("@playercount", playercount);
                command.Parameters.AddWithValue("@ServerId", ServerId);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task RemoveAllGameServers()
    {
        using (var connection = new SQLiteConnection($"Data Source={_settings.DatabaseName};Version=3;"))
        {
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM GameServer";
                await command.ExecuteNonQueryAsync();
            }
        }

        _settings.GameServerPortPool = 5100;
    }

}