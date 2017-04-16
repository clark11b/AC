﻿using ACE.Entity;
using ACE.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE.Network;

namespace ACE.Factories
{
    public class MonsterFactory
    {
        public static WorldObject CreateMonster(uint templateId, Position position)
        {
            // TODO: Implement

            // read template from the database, create an object
            // do whatever else it takes to make a monster

            // assign it the position

            return null;
        }

        /// <summary>
        /// Create a new creature at the specified position
        /// </summary>
        /// <param name="saveAsStatic">If set to true, it saves the spawned creature in the DB as a static spawn</param>
        public static Creature SpawnCreature(uint weenieClassId, bool saveAsStatic, Position position)
        {
            AceCreatureObject aco = DatabaseManager.World.GetCreatureDataByWeenie(weenieClassId);
            if (aco == null)
                return null;
            
            AceCreatureStaticLocation acsl = new AceCreatureStaticLocation();
            acsl.WeenieClassId = (ushort)weenieClassId;
            acsl.Landblock = (ushort)position.LandblockId.Landblock;
            acsl.Cell = (ushort)position.Cell;
            acsl.PosX = position.PositionX;
            acsl.PosY = position.PositionY;
            acsl.PosZ = position.PositionZ;
            acsl.QW = position.RotationW;
            acsl.QX = position.RotationX;
            acsl.QY = position.RotationY;
            acsl.QZ = position.RotationZ;
            acsl.CreatureData = aco;

            Creature newCreature = new Creature(acsl);

            if (saveAsStatic) {
                bool success = DatabaseManager.World.InsertStaticCreatureLocation(acsl);
                if (!success)
                    return null;
            }

            return newCreature;
        }
    }
}
