using System;
using System.Collections.Generic;
using System.Linq;
using ACE.Common;
using ACE.Common.Extensions;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.WorldObjects;

namespace ACE.Server.Managers
{
    public class QuestManager
    {
        public Player Player { get; }
        public ICollection<CharacterPropertiesQuestRegistry> Quests { get => Player.Character.CharacterPropertiesQuestRegistry; }

        /// <summary>
        /// Constructs a new QuestManager for a Player
        /// </summary>
        public QuestManager(Player player)
        {
            Player = player;
        }

        /// <summary>
        /// Returns TRUE if a player has started a particular quest
        /// </summary>
        public bool HasQuest(String questName)
        {
            return GetQuest(questName) != null;
        }

        /// <summary>
        /// Returns an active or completed quest for this player
        /// </summary>
        public CharacterPropertiesQuestRegistry GetQuest(string questName)
        {
            return Quests.FirstOrDefault(q => q.QuestName.Equals(questName));
        }

        /// <summary>
        /// Adds or updates a quest completion to the player's registry
        /// </summary>
        public void Update(string questName)
        {
            var existing = Quests.FirstOrDefault(q => q.QuestName == questName);

            if (existing == null)
            {
                // add new quest entry
                var quest = new CharacterPropertiesQuestRegistry
                {
                    QuestName = questName,
                    CharacterId = Player.Guid.Full,
                    LastTimeCompleted = (uint)Time.GetUnixTime(),
                    NumTimesCompleted = 1   // initial add / first solve
                };
                Quests.Add(quest);
            }
            else
            {
                // update existing quest
                existing.LastTimeCompleted = (uint)Time.GetUnixTime();
                existing.NumTimesCompleted++;
            }
        }

        /// <summary>
        /// Returns TRUE if player can solve this quest now
        /// </summary>
        public bool CanSolve(string questName)
        {
            // verify max solves / quest timer
            var nextSolveTime = GetNextSolveTime(questName);

            return nextSolveTime == TimeSpan.MinValue;
        }

        /// <summary>
        /// Returns TRUE if player has reached the maximum # of solves for this quest
        /// </summary>
        public bool IsMaxSolves(string questName)
        {
            var quest = DatabaseManager.World.GetCachedQuest(questName);
            if (quest == null) return false;

            var playerQuest = GetQuest(questName);
            if (playerQuest == null) return false;  // player hasn't completed this quest yet

            // return TRUE if quest has solve limit, and it has been reached
            return quest.MaxSolves > -1 && playerQuest.NumTimesCompleted >= quest.MaxSolves;
        }

        /// <summary>
        /// Returns the time remaining until the player can solve this quest again
        /// </summary>
        public TimeSpan GetNextSolveTime(string questName)
        {
            var quest = DatabaseManager.World.GetCachedQuest(questName);
            if (quest == null)
                return TimeSpan.MaxValue;   // world quest not found - cannot solve it

            var playerQuest = GetQuest(questName);
            if (playerQuest == null)
                return TimeSpan.MinValue;   // player hasn't completed this quest yet - can solve immediately

            if (quest.MaxSolves > -1 && playerQuest.NumTimesCompleted >= quest.MaxSolves)
                return TimeSpan.MaxValue;   // cannot solve this quest again - max solves reached / exceeded

            var currentTime = (uint)Time.GetUnixTime();
            var nextSolveTime = playerQuest.LastTimeCompleted + quest.MinDelta;

            if (currentTime >= nextSolveTime)
                return TimeSpan.MinValue;   // can solve again now - next solve time expired

            // return the time remaining on the player's quest timer
            return TimeSpan.FromSeconds(nextSolveTime - currentTime);
        }

        /// <summary>
        /// Increment the number of times completed for a quest
        /// </summary>
        public void Increment(string questName)
        {
            // kill task / append # to quest name?
            Update(questName);
        }

        /// <summary>
        /// Removes an existing quest from the Player's registry
        /// </summary>
        public void Erase(string questName)
        {
            //Console.WriteLine("QuestManager.Erase: " + questName);

            var quests = Quests.Where(q => q.QuestName.Equals(questName)).ToList();
            foreach (var quest in quests)
                Quests.Remove(quest);
        }

        /// <summary>
        /// Shows the current quests in progress for a Player
        /// </summary>
        public void ShowQuests(Player player)
        {
            Console.WriteLine("ShowQuests");

            if (Quests.Count == 0)
            {
                Console.WriteLine("No quests in progress for " + player.Name);
                return;
            }
            foreach (var quest in Quests)
            {
                Console.WriteLine("Quest Name: " + quest.QuestName);
                Console.WriteLine("Times Completed: " + quest.NumTimesCompleted);
                Console.WriteLine("Last Time Completed: " + quest.LastTimeCompleted);
                Console.WriteLine("Quest ID: " + quest.Id.ToString("X8"));
                Console.WriteLine("Player ID: " + quest.CharacterId.ToString("X8"));
                Console.WriteLine("----");
            }
        }

        public void Stamp(string questName)
        {
            // ?
            Update(questName);
        }

        public void SendNetworkMessage(string questName)
        {
            if (IsMaxSolves(questName))
            {
                var error = new GameEventInventoryServerSaveFailed(Player.Session, WeenieError.YouHaveSolvedThisQuestTooManyTimes);
                var text = new GameMessageSystemChat("You have solved this quest too many times!", ChatMessageType.Broadcast);
                Player.Session.Network.EnqueueSend(text, error);
            }
            else
            {
                var error = new GameEventInventoryServerSaveFailed(Player.Session, WeenieError.YouHaveSolvedThisQuestTooRecently);
                var text = new GameMessageSystemChat("You have solved this quest too recently!", ChatMessageType.Broadcast);

                var remainStr = GetNextSolveTime(questName).GetFriendlyString();
                var remain = new GameMessageSystemChat($"You may complete this quest again in {remainStr}.", ChatMessageType.Broadcast);
                Player.Session.Network.EnqueueSend(text, remain, error);
            }
        }

        public void SendNetworkMessageNoQuest(WorldObject wo)
        {
            if (wo is Portal)
            {
                var error = new GameEventInventoryServerSaveFailed(Player.Session, WeenieError.YouMustCompleteQuestToUsePortal);
                var text = new GameMessageSystemChat("You must complete a quest to interact with that portal.", ChatMessageType.Broadcast);
                Player.Session.Network.EnqueueSend(text, error);
            }
            else
            {
                var error = new GameEventInventoryServerSaveFailed(Player.Session, WeenieError.ItemRequiresQuestToBePickedUp);
                var text = new GameMessageSystemChat("This item requires you to complete a specific quest before you can pick it up!", ChatMessageType.Broadcast);
                Player.Session.Network.EnqueueSend(text, error);
            }
        }
    }
}
