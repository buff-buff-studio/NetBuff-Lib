using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Packets
{
    /// <summary>
    /// Packet used to spawn a network object.
    /// </summary>
    public class NetworkObjectSpawnPacket : IPacket
    {
        /// <summary>
        /// The network id of the network object.
        /// </summary>
        public NetworkId Id { get; set; }

        /// <summary>
        /// The network id of the prefab.
        /// </summary>
        public NetworkId PrefabId { get; set; }

        /// <summary>
        /// The owner id of the network object.
        /// If the owner id is -1, the object will be owned by the server.
        /// </summary>
        public int OwnerId { get; set; }

        /// <summary>
        /// The position of the network object.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// The rotation of the network object.
        /// </summary>
        public Quaternion Rotation { get; set; }

        /// <summary>
        /// The scale of the network object.
        /// </summary>
        public Vector3 Scale { get; set; }

        /// <summary>
        /// The active state of the network object.
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// The scene id of the network object.
        /// If the scene id is 0, the object will be spawned in the main scene.
        /// If the scene id is -1, the object will be spawned in the last loaded scene.
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
            writer.Write(IsActive);
            writer.Write(SceneId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            PrefabId = NetworkId.Read(reader);
            OwnerId = reader.ReadInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle());
            Scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            IsActive = reader.ReadBoolean();
            SceneId = reader.ReadInt32();
        }
    }
}