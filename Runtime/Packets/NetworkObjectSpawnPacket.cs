using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Packets
{
    /// <summary>
    /// Used to sync the spawn of a network object over the network
    /// </summary>
    public class NetworkObjectSpawnPacket : IPacket
    {
        /// <summary>
        /// The id of the network object
        /// </summary>
        public NetworkId Id { get; set; }
        
        /// <summary>
        /// The id of the prefab of the network object
        /// </summary>
        public NetworkId PrefabId { get; set; }
        
        /// <summary>
        /// The id of the owner of the network object
        /// If the owner id is -1, the object is owned by the server
        /// </summary>
        public int OwnerId { get; set; }
        
        /// <summary>
        /// The position of the network object
        /// </summary>
        public Vector3 Position { get; set; }
        
        /// <summary>
        /// The rotation of the network object
        /// </summary>
        public Quaternion Rotation { get; set; }
        
        /// <summary>
        /// The scale of the network object
        /// </summary>
        public Vector3 Scale { get; set; }
        
        /// <summary>
        /// The active state of the network object
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// True if the object is being spawned to a mid/late joiner
        /// </summary>
        public bool IsRetroactive { get; set; }
        
        /// <summary>
        /// The current scene id of the network object
        /// </summary>
        public int SceneId { get; set; } 
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            PrefabId.Serialize(writer);
            writer.Write(OwnerId);
            writer.Write(Position.x);
            writer.Write(Position.y);
            writer.Write(Position.z);
            writer.Write(Rotation.x);
            writer.Write(Rotation.y);
            writer.Write(Rotation.z);
            writer.Write(Rotation.w);
            writer.Write(Scale.x);
            writer.Write(Scale.y);
            writer.Write(Scale.z);
            writer.Write(IsRetroactive);
            writer.Write(IsActive);
            writer.Write(SceneId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            PrefabId = NetworkId.Read(reader);
            OwnerId = reader.ReadInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            IsRetroactive = reader.ReadBoolean();
            IsActive = reader.ReadBoolean();
            SceneId = reader.ReadInt32();
        }
    }
}