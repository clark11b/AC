using System;
using System.Linq;

using ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Network.Structure;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    public class SquelchManager
    {
        /// <summary>
        /// The player who owns these squelches
        /// </summary>
        public Player Player;

        /// <summary>
        /// The SquelchDB contains the account and character squelches,
        /// in the network protocol Dictionary format
        /// </summary>
        public SquelchDB Squelches;

        /// <summary>
        /// Constructs a new SquelchManager for a Player
        /// </summary>
        /// <param name="player"></param>
        public SquelchManager(Player player)
        {
            Player = player;

            UpdateSquelchDB();
        }

        /// <summary>
        /// Returns TRUE if this player has squelched any accounts or characters
        /// </summary>
        public bool HasSquelches => Squelches.Accounts.Count > 0 || Squelches.Characters.Count > 0;

        /// <summary>
        /// Returns TRUE if this channel can be squelched
        /// </summary>
        public static bool IsLegalChannel(ChatMessageType channel)
        {
            switch (channel)
            {
                case ChatMessageType.AllChannels:   // added?
                case ChatMessageType.Speech:
                case ChatMessageType.Tell:
                case ChatMessageType.Combat:
                case ChatMessageType.Magic:
                case ChatMessageType.Emote:
                case ChatMessageType.Appraisal:
                case ChatMessageType.Spellcasting:
                case ChatMessageType.Allegiance:
                case ChatMessageType.Fellowship:
                case ChatMessageType.CombatEnemy:
                case ChatMessageType.CombatSelf:
                case ChatMessageType.Recall:
                case ChatMessageType.Craft:
                case ChatMessageType.Salvaging:

                    return true;
            }
            return false;
        }

        /// <summary>
        /// Called when adding or removing a character squelch
        /// </summary>
        public void HandleActionModifyCharacterSquelch(bool squelch, uint playerGuid, string playerName, ChatMessageType messageType)
        {
            Console.WriteLine($"{Player.Name}.HandleActionModifyCharacterSquelch({squelch}, {playerGuid:X8}, {playerName}, {messageType})");

            if (!IsLegalChannel(messageType))
            {
                Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{messageType} is not a legal squelch channel", ChatMessageType.Broadcast));
                return;
            }

            IPlayer player;

            if (playerGuid != 0)
            {
                player = PlayerManager.FindByGuid(new ObjectGuid(playerGuid));

                if (player == null)
                {
                    Player.Session.Network.EnqueueSend(new GameMessageSystemChat("Couldn't find player to squelch.", ChatMessageType.Broadcast));
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(playerName)) return;

                player = PlayerManager.FindByName(playerName);

                if (player == null)
                {
                    Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{playerName} not found.", ChatMessageType.Broadcast));
                    return;
                }
            }

            if (player.Guid == Player.Guid)
            {
                Player.Session.Network.EnqueueSend(new GameMessageSystemChat("You can't squelch yourself!", ChatMessageType.Broadcast));
                return;
            }

            var squelches = Player.Character.GetSquelches(Player.CharacterDatabaseLock);

            var existing = squelches.FirstOrDefault(i => i.SquelchCharacterId == player.Guid.Full);

            if (squelch)
            {
                if (existing != null)
                {
                    Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} is already squelched.", ChatMessageType.Broadcast));
                    return;
                }

                Player.Character.AddOrUpdateSquelch(player.Guid.Full, 0, (uint)messageType, Player.CharacterDatabaseLock);
                Player.CharacterChangesDetected = true;

                Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} has been squelched.", ChatMessageType.Broadcast));
            }
            else
            {
                if (existing == null)
                {
                    Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} is not squelched.", ChatMessageType.Broadcast));
                    return;
                }

                Player.Character.TryRemoveSquelch(player.Guid.Full, 0, out _, Player.CharacterDatabaseLock);
                Player.CharacterChangesDetected = true;

                Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name} has been unsquelched.", ChatMessageType.Broadcast));
            }

            UpdateSquelchDB();

            SendSquelchDB();
        }

        /// <summary>
        /// Called when adding or removing an account squelch
        /// </summary>
        public void HandleActionModifyAccountSquelch(bool squelch, string playerName)
        {
            Console.WriteLine($"{Player.Name}.HandleActionModifyAccountSquelch({squelch}, {playerName})");

            if (string.IsNullOrWhiteSpace(playerName)) return;

            var player = PlayerManager.FindByName(playerName);

            if (player == null)
            {
                Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{playerName} not found.", ChatMessageType.Broadcast));
                return;
            }

            if (player.Account.AccountId == Player.Account.AccountId)
            {
                Player.Session.Network.EnqueueSend(new GameMessageSystemChat("You can't squelch yourself!", ChatMessageType.Broadcast));
                return;
            }

            var squelches = Player.Character.GetSquelches(Player.CharacterDatabaseLock);

            var existing = squelches.FirstOrDefault(i => i.SquelchAccountId == player.Account.AccountId);

            if (squelch)
            {
                if (existing != null)
                {
                    Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name}'s account is already squelched.", ChatMessageType.Broadcast));
                    return;
                }

                // always all channels?
                Player.Character.AddOrUpdateSquelch(player.Guid.Full, player.Account.AccountId, (uint)ChatMessageType.AllChannels, Player.CharacterDatabaseLock);
                Player.CharacterChangesDetected = true;

                Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name}'s account has been squelched.", ChatMessageType.Broadcast));
            }
            else
            {
                if (existing == null)
                {
                    Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name}'s account is not squelched.", ChatMessageType.Broadcast));
                    return;
                }

                Player.Character.TryRemoveSquelch(existing.SquelchCharacterId, player.Account.AccountId, out _, Player.CharacterDatabaseLock);
                Player.CharacterChangesDetected = true;

                Player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{player.Name}'s account has been unsquelched.", ChatMessageType.Broadcast));
            }

            UpdateSquelchDB();

            SendSquelchDB();
        }

        /// <summary>
        /// Called when modifying the global squelches (entry point?)
        /// </summary>
        public void HandleActionModifyGlobalSquelch(bool squelch, ChatMessageType messageType)
        {
            Console.WriteLine($"{Player.Name}.HandleActionModifyGlobalSquelch({squelch}, {messageType})");
        }

        /// <summary>
        /// Builds the SquelchDB for network sending
        /// </summary>
        /// <returns></returns>
        public void UpdateSquelchDB()
        {
            Squelches = new SquelchDB(Player.Character.GetSquelches(Player.CharacterDatabaseLock));
        }

        /// <summary>
        /// Sends the SquelchDB to the player
        /// </summary>
        public void SendSquelchDB()
        {
            Player.Session.Network.EnqueueSend(new GameEventSetSquelchDB(Player.Session, Squelches));
        }
    }
}
