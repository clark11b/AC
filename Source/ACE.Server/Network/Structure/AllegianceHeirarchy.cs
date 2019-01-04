using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using ACE.Entity;
using ACE.Server.Network.Enum;

namespace ACE.Server.Network.Structure
{
    public class AllegianceHeirarchy
    {
        public AllegianceProfile Profile;

        public AllegianceHeirarchy(AllegianceProfile profile)
        {
            Profile = profile;
        }
    }

    public static class AllegianceHeirarchyExtensions
    {
        public static void Write(this BinaryWriter writer, AllegianceHeirarchy heirarchy)
        {
            // ushort - recordCount - Number of character allegiance records
            // ushort - oldVersion = 0x000B - Defines which properties are available. 0x000B seems to be the latest version which includes all properties.
            // Dictionary<ObjectID, AllegianceOfficerLevel> - officers - Taking a guess on these values. Guessing they may only be valid for Monarchs
            //                                                           A list of officers and their officer levels?
            // List<string> - officerTitles - Believe these may pass in the current officer title list. Guessing they may only be valid on Monarchs.
            // uint - monarchBroadcastTime - May only be valid for Monarchs/Speakers?
            // uint - monarchBroadcastsToday - May only be valid for Monarchs/Speakers?
            // uint - spokesBroadcastTime - May only be valid for Monarchs/Speakers?
            // uint - spokesBroadcastsToday - May only be valid for Monarchs/Speakers?
            // string - motd - Text for current Message of the Day. May only be valid for Monarchs/Speakers?
            // string - motdSetBy - Who set the current Message of the Day. May only be valid for Monarchs/Speakers?
            // uint - chatRoomID - allegiance chat channel number
            // Position - bindpoint - Location of monarchy bindpoint
            // string - allegianceName - The name of the allegiance.
            // uint - nameLastSetTime - Time name was last set. Seems to count upward for some reason.
            // bool - isLocked - Whether allegiance is locked.
            // int - approvedVassal - ??
            // AllegianceData - monarchData - Monarch's data

            // records: vector of length recordCount - 1
            // ObjectID - treeParent - The ObjectID for the parent character to this character. Used by the client to decide how to build the display in the Allegiance tab. 1 is the monarch.
            // AllegianceData - allegianceData

            // recordCount = Monarch + Patron + Vassals?
            // 2 in data for small allegiances?
            ushort recordCount = 0;
            ushort oldVersion = 0x000B;
            var officers = new Dictionary<ObjectGuid, AllegianceOfficerLevel>();
            var officerTitles = new List<string>();
            uint monarchBroadcastTime = 0;
            uint monarchBroadcastsToday = 0;
            uint spokesBroadcastTime = 0;
            uint spokesBroadcastsToday = 0;
            var motd = "";
            var motdSetBy = "";
            uint chatRoomID = 0;
            var bindPoint = new Position();
            var allegianceName = "";
            uint nameLastSetTime = 0;
            bool isLocked = false;
            int approvedVassal = 0;
            AllegianceData monarchData = null;
            List<Tuple<ObjectGuid, AllegianceData>> records = null;

            var allegiance = heirarchy.Profile.Allegiance;
            var node = heirarchy.Profile.Node;

            if (allegiance != null && node != null)
            {
                // aclogview (verify):
                // i == 0 : monarch (no guid)
                // i == 1 : patron
                // i == 2 : peer?
                // i  > 2 : vassals

                // peers = others with the same patron?

                recordCount = 1;    // monarch
                if (node.Patron != null && !node.Patron.IsMonarch)  // patron
                    recordCount++;
                if (!node.IsMonarch)    // self
                    recordCount++;
                if (node.TotalVassals > 0)  // vassals
                {
                    recordCount += (ushort)node.TotalVassals;
                }
                //Console.WriteLine("Records: " + recordCount);

                var monarch = allegiance.Monarch.Player;

                chatRoomID = monarch.Guid.Full;
                allegianceName = monarch.Name;

                // monarch
                monarchData = new AllegianceData(allegiance.Monarch);

                if (recordCount > 1)
                {
                    records = new List<Tuple<ObjectGuid, AllegianceData>>();

                    // patron
                    if (node.Patron != null && !node.Patron.IsMonarch)
                    {
                        records.Add(new Tuple<ObjectGuid, AllegianceData>(node.Monarch.PlayerGuid, new AllegianceData(node.Patron)));
                    }

                    // self
                    if (!node.IsMonarch)
                        records.Add(new Tuple<ObjectGuid, AllegianceData>(node.Patron.PlayerGuid, new AllegianceData(node)));

                    // vassals
                    if (node.TotalVassals > 0)
                    {
                        foreach (var vassal in node.Vassals)
                            records.Add(new Tuple<ObjectGuid, AllegianceData>(node.PlayerGuid, new AllegianceData(vassal)));
                    }
                }
            }

            writer.Write(recordCount);
            writer.Write(oldVersion);
            writer.Write(officers);
            writer.Write(officerTitles);
            writer.Write(monarchBroadcastTime);
            writer.Write(monarchBroadcastsToday);
            writer.Write(spokesBroadcastTime);
            writer.Write(spokesBroadcastsToday);
            writer.WriteString16L(motd);
            writer.WriteString16L(motdSetBy);
            writer.Write(chatRoomID);
            writer.Write(bindPoint);
            writer.WriteString16L(allegianceName);
            writer.Write(nameLastSetTime);
            writer.Write(Convert.ToUInt32(isLocked));
            writer.Write(approvedVassal);

            if (monarchData != null)
                writer.Write(monarchData);

            if (records != null)
                writer.Write(records);
        }

        public static void Write(this BinaryWriter writer, Dictionary<ObjectGuid, AllegianceOfficerLevel> officers)
        {
            PHashTable.WriteHeader(writer, officers.Count);

            foreach (var officer in officers)
            {
                writer.Write(officer.Key.Full);
                writer.Write((uint)officer.Value);
            }
        }

        public static void Write(this BinaryWriter writer, List<string> strings)
        {
            writer.Write(strings.Count);
            foreach (var str in strings)
                writer.WriteString16L(str);
        }

        public static void Write(this BinaryWriter writer, Position position)
        {
            writer.Write(position.Cell);
            writer.Write(position.Pos);
            writer.Write(position.Rotation);
        }

        public static void Write(this BinaryWriter writer, Vector3 v)
        {
            writer.Write(v.X);
            writer.Write(v.Y);
            writer.Write(v.Z);
        }

        public static void Write(this BinaryWriter writer, Quaternion q)
        {
            writer.Write(q.W);
            writer.Write(q.X);
            writer.Write(q.Y);
            writer.Write(q.Z);
        }

        public static void Write(this BinaryWriter writer, List<Tuple<ObjectGuid, AllegianceData>> records)
        {
            //writer.Write(records.Count);
            foreach (var record in records)
            {
                writer.Write(record.Item1.Full);
                writer.Write(record.Item2);
            }
        }

        /// <summary>
        /// Returns the number of bits required to store the input number
        /// </summary>
        public static uint GetNumBits(uint num)
        {
            return (uint)Math.Log(num, 2) + 1;
        }
    }
}
