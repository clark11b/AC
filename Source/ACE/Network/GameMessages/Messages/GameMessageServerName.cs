﻿using ACE.Entity.Enum;

namespace ACE.Network.GameMessages.Messages
{
    public class GameMessageServerName : GameMessage
    {
        public GameMessageServerName(string serverName, int currentConnections = 0, int maxConnections = -1)
            : base(GameMessageOpcode.ServerName, GameMessageGroup.Group09)
        {
            Writer.Write(currentConnections);
            Writer.Write(maxConnections);
            Writer.WriteString16L(serverName);
        }
    }
}
