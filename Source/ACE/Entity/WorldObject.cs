using ACE.Entity.Enum;
using ACE.Network;
using ACE.Network.Enum;
using ACE.Network.GameMessages.Messages;
using ACE.Network.Sequence;
using System.IO;
using ACE.Managers;
using ACE.Network.Motion;
using log4net;

namespace ACE.Entity
{
    public abstract class WorldObject
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ObjectGuid Guid { get; }

        public ObjectType Type { get; protected set; }

        /// <summary>
        /// wcid - stands for weenie class id
        /// </summary>
        public uint WeenieClassid { get; protected set; }

        public uint Icon { get; set; }

        public string Name { get; protected set; }

        /// <summary>
        /// tick-stamp for the last time this object changed in any way.
        /// </summary>
        public double LastUpdatedTicks { get; set; }

        // <summary>
        /// Time when this object will despawn, -1 is never.
        /// </summary>
        public double DespawnTime { get; set; } = -1;

        public virtual Position Location
        {
            get { return PhysicsData.Position; }
            protected set
            {
                if (PhysicsData.Position != null)
                    LastUpdatedTicks = WorldManager.PortalYearTicks;

                log.Debug($"{Name} moved to {PhysicsData.Position}");

                PhysicsData.Position = value;
            }
        }

        /// <summary>
        /// tick-stamp for the last time a movement update was sent
        /// </summary>
        public double LastMovementBroadcastTicks { get; set; }

        /// <summary>
        /// tick-stamp for the server time of the last time the player moved.
        /// TODO: implement
        /// </summary>
        public double LastAnimatedTicks { get; set; }

        public ObjectDescriptionFlag DescriptionFlags { get; protected set; }

        public WeenieHeaderFlag WeenieFlags { get; protected set; }

        public WeenieHeaderFlag2 WeenieFlags2 { get; protected set; }

        public UpdatePositionFlag PositionFlag { get; set; }

        public CombatMode CombatMode { get; private set; }

        public virtual void PlayScript(Session session) { }

        public virtual float ListeningRadius { get; protected set; } = 5f;

        public ModelData ModelData { get; }

        public PhysicsData PhysicsData { get; }

        public GameData GameData { get; }

        public SequenceManager Sequences { get; }

        protected WorldObject(ObjectType type, ObjectGuid guid)
        {
            Type = type;
            Guid = guid;

            GameData = new GameData();
            ModelData = new ModelData();

            Sequences = new SequenceManager();
            Sequences.AddOrSetSequence(SequenceType.ObjectPosition, new UShortSequence());
            Sequences.AddOrSetSequence(SequenceType.ObjectMovement, new UShortSequence());
            Sequences.AddOrSetSequence(SequenceType.ObjectState, new UShortSequence());
            Sequences.AddOrSetSequence(SequenceType.ObjectVector, new UShortSequence());
            Sequences.AddOrSetSequence(SequenceType.ObjectTeleport, new UShortSequence());
            Sequences.AddOrSetSequence(SequenceType.ObjectServerControl, new UShortSequence());
            Sequences.AddOrSetSequence(SequenceType.ObjectForcePosition, new UShortSequence());
            Sequences.AddOrSetSequence(SequenceType.ObjectVisualDesc, new UShortSequence());
            Sequences.AddOrSetSequence(SequenceType.ObjectInstance, new UShortSequence());
            Sequences.AddOrSetSequence(SequenceType.Motion, new UShortSequence(1));

            PhysicsData = new PhysicsData(Sequences);
        }

        public void SetCombatMode(CombatMode newCombatMode)
        {
            log.InfoFormat("Changing combat mode for {0} to {1}", this.Guid, newCombatMode);
            // TODO: any sort of validation
            CombatMode = newCombatMode;
            switch (CombatMode)
            {
                case CombatMode.Peace:
                    SetMotionState(new UniversalMotion(MotionStance.Standing));
                    break;
                case CombatMode.Melee:
                    var gm = new UniversalMotion(MotionStance.UANoShieldAttack);
                    gm.MovementData.CurrentStyle = (ushort)MotionStance.UANoShieldAttack;
                    SetMotionState(gm);
                    break;
            }
        }

        public void SetMotionState(MotionState motionState)
        {
            Player p = (Player)this;
            PhysicsData.CurrentMotionState = motionState;
            var updateMotion = new GameMessageUpdateMotion(this, p.Session, motionState);
            p.Session.Network.EnqueueSend(updateMotion);
        }

        public virtual void SerializeUpdateObject(BinaryWriter writer)
        {
            // content of these 2 is the same? TODO: Validate that?
            SerializeCreateObject(writer);
        }

        public virtual void SerializeCreateObject(BinaryWriter writer)
        {
            writer.WriteGuid(Guid);

            ModelData.Serialize(writer);
            PhysicsData.Serialize(this, writer);

            writer.Write((uint)WeenieFlags);
            writer.WriteString16L(Name);
            writer.WritePackedDword(WeenieClassid);
            writer.WritePackedDwordOfKnownType(Icon, 0x6000000);
            writer.Write((uint)Type);
            writer.Write((uint)DescriptionFlags);
            writer.Align();

            if ((DescriptionFlags & ObjectDescriptionFlag.AdditionFlags) != 0)
                writer.Write((uint)WeenieFlags2);

            if ((WeenieFlags & WeenieHeaderFlag.PuralName) != 0)
                writer.WriteString16L(GameData.NamePlural);

            if ((WeenieFlags & WeenieHeaderFlag.ItemCapacity) != 0)
            {
                if (GameData.ItemCapacity != null)
                {
                    writer.Write((byte)GameData.ItemCapacity);
                }
                else
                {
                    // This is really a safety measure to catch any case were we set a flag to
                    // write data - but did not assign any value to the field.  At some point this
                    // should come out.   Not going to repeat this comment but it applies to all of these.
                    writer.Write((byte)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.ContainerCapacity) != 0)
            {
                if (GameData.ContainerCapacity != null)
                {
                    writer.Write((byte)GameData.ContainerCapacity);
                }
                else
                {
                    writer.Write((byte)0);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.AmmoType) != 0)
            {
                if (GameData.AmmoType != null)
                {
                    writer.Write((ushort)GameData.AmmoType);
                }
                else
                {
                    writer.Write((ushort)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Value) != 0)
            {
                if (GameData.Value != null)
                {
                    writer.Write((uint)GameData.Value);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Usable) != 0)
            {
                if (GameData.Usable != null)
                {
                    writer.Write((uint)GameData.Usable);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.UseRadius) != 0)
            {
                if (GameData.UseRadius != null)
                {
                    writer.Write((float)GameData.UseRadius);
                }
                else
                {
                    writer.Write((float)0.00f);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.TargetType) != 0)
            {
                if (GameData.TargetType != null)
                {
                    writer.Write((uint)GameData.TargetType);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.UiEffects) != 0)
            {
                if (GameData.UiEffects != null)
                {
                    writer.Write((uint)GameData.UiEffects);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.CombatUse) != 0)
            {
                if (GameData.CombatUse != null)
                {
                    writer.Write((byte)GameData.CombatUse);
                }
                else
                {
                    writer.Write((byte)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Struture) != 0)
            {
                if (GameData.Structure != null)
                {
                    writer.Write((ushort)GameData.Structure);
                }
                else
                {
                    writer.Write((ushort)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.MaxStructure) != 0)
            {
                if (GameData.ItemCapacity != null)
                {
                    writer.Write((ushort)GameData.MaxStructure);
                }
                else
                {
                    writer.Write((ushort)0);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.StackSize) != 0)
            {
                if (GameData.StackSize != null)
                {
                    writer.Write((ushort)GameData.StackSize);
                }
                else
                {
                    writer.Write((ushort)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.MaxStackSize) != 0)
            {
                if (GameData.MaxStackSize != null)
                {
                    writer.Write((ushort)GameData.MaxStackSize);
                }
                else
                {
                    writer.Write((ushort)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Container) != 0)
            {
                if (GameData.ContainerId != null)
                {
                    writer.Write((uint)GameData.ContainerId);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Wielder) != 0)
            {
                if (GameData.Wielder != null)
                {
                    writer.Write((uint)GameData.Wielder);
                }
                else
                {
                    writer.Write((uint)0);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.ValidLocations) != 0)
            {
                if (GameData.ValidLocations != null)
                {
                    writer.Write((uint)GameData.ValidLocations);
                }
                else
                {
                    writer.Write((uint)0);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Location) != 0)
            {
                if (GameData.Location != null)
                {
                    writer.Write((uint)GameData.Location);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Priority) != 0)
            {
                if (GameData.Priority != null)
                {
                    writer.Write((uint)GameData.Priority);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.BlipColour) != 0)
            {
                if (GameData.RadarColour != null)
                {
                    writer.Write((byte)GameData.RadarColour);
                }
                else
                {
                    writer.Write((byte)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.RadarBehavior) != 0)
            {
                if (GameData.RadarBehavior != null)
                {
                    writer.Write((byte)GameData.RadarBehavior);
                }
                else
                {
                    writer.Write((byte)GameData.RadarBehavior);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Script) != 0)
            {
                if (GameData.Script != null)
                {
                    writer.Write((ushort)GameData.Script);
                }
                else
                {
                    writer.Write((ushort)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Workmanship) != 0)
            {
                if (GameData.Workmanship != null)
                {
                    writer.Write((float)GameData.Workmanship);
                }
                else
                {
                    writer.Write((float)0.00f);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Burden) != 0)
            {
                if (GameData.Burden != null)
                {
                    writer.Write((ushort)GameData.Burden);
                }
                else
                {
                    writer.Write((ushort)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Spell) != 0)
            {
                if (GameData.Spell != null)
                {
                    writer.Write((uint)GameData.Spell);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.HouseOwner) != 0)
            {
                if (GameData.HouseOwner != null)
                {
                    writer.Write((uint)GameData.HouseOwner);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            /*if ((WeenieFlags & WeenieHeaderFlag.HouseRestrictions) != 0)
            {
            }*/

            if ((WeenieFlags & WeenieHeaderFlag.HookItemTypes) != 0)
            {
                if (GameData.HookItemTypes != null)
                {
                    writer.Write((uint)GameData.HookItemTypes);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.Monarch) != 0)
            {
                if (GameData.Monarch != null)
                {
                    writer.Write((uint)GameData.Monarch);
                }
                else
                {
                    writer.Write((uint)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.HookType) != 0)
            {
                if (GameData.HookType != null)
                {
                    writer.Write((ushort)GameData.HookType);
                }
                else
                {
                    writer.Write((ushort)0u);
                }
            }

            if ((WeenieFlags & WeenieHeaderFlag.IconOverlay) != 0)
                writer.WritePackedDwordOfKnownType(GameData.IconOverlay, 0x6000000);

            if ((WeenieFlags2 & WeenieHeaderFlag2.IconUnderlay) != 0)
                writer.WritePackedDwordOfKnownType(GameData.IconUnderlay, 0x6000000);

            if (((WeenieFlags & WeenieHeaderFlag.Material) != 0) && GameData.Monarch != null)
                writer.Write((uint)GameData.Material);

            /*if ((WeenieFlags2 & WeenieHeaderFlag2.Cooldown) != 0)
                writer.Write(0u);*/

            /*if ((WeenieFlags2 & WeenieHeaderFlag2.CooldownDuration) != 0)
                writer.Write(0.0d);*/

            /*if ((WeenieFlags2 & WeenieHeaderFlag2.PetOwner) != 0)
                writer.Write(0u);*/

            writer.Align();
        }

        public void WriteUpdatePositionPayload(BinaryWriter writer)
        {
            writer.WriteGuid(Guid);
            Location.Serialize(writer, PositionFlag);
            writer.Write(Sequences.GetCurrentSequence(SequenceType.ObjectInstance));
            writer.Write(Sequences.GetNextSequence(SequenceType.ObjectPosition));
            writer.Write(Sequences.GetCurrentSequence(SequenceType.ObjectTeleport));
            writer.Write(Sequences.GetCurrentSequence(SequenceType.ObjectForcePosition));
        }
    }
}
