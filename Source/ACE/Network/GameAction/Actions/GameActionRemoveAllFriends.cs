﻿
namespace ACE.Network.GameAction.Actions
{
    [GameAction(GameActionOpcode.RemoveAllFriends)]
    public class GameActionRemoveAllFriends : GameActionPacket
    {
        public GameActionRemoveAllFriends(Session session, ClientPacketFragment fragment) : base(session, fragment) { }

        public override void Handle()
        {
            Session.Player.RemoveAllFriends();
        }
    }
}
