﻿namespace MasterServer;

public class GameServer
{
    public bool isActive {get; set;}
    public string ipAddress { get; set; }
    public int port { get; set; }
    public int playerCount { get; set; }
    public int maxCapacity { get; set; }
    public string ServerId { get; set; }
    public string instanceId { get; set; }

    public GameServer(string ipAddress, int port, int playerCount, int maxCapacity, string instanceId, bool isActive)
    {
        this.instanceId = instanceId;
        this.ipAddress = ipAddress;
        this.port = port;
        this.playerCount = playerCount;
        this.maxCapacity = maxCapacity;
        this.isActive = isActive;
    }

    public GameServer()
    {
        isActive = true;
        ipAddress = "";
        port = 0;
        playerCount = 0;
        maxCapacity = 0;
        instanceId = "";
        ServerId = "";
    }
}