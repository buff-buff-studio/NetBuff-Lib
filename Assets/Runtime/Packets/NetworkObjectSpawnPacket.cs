using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Packets
{
    public class NetworkObjectSpawnPacket : IPacket
    {
        [NetworkIdInspectorMode(NetworkIdInspectorMode.Object)]
        public NetworkId Id { get; set; }

        [NetworkIdInspectorMode(NetworkIdInspectorMode.Prefab)]
        public NetworkId PrefabId { get; set; }

        [NetworkIdInspectorMode(NetworkIdInspectorMode.Owner)]
        public int OwnerId { get; set; }

        public Vector3 Position { get; set; }

        public Quaternion Rotation { get; set; }

        public Vector3 Scale { get; set; }

        public bool IsActive { get; set; }

        [NetworkIdInspectorMode(NetworkIdInspectorMode.Scene)]
        public int SceneId { get; set; }

        public NetworkId EventId { get; set; } = NetworkId.Empty;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(PrefabId);
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
            writer.Write(EventId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
            PrefabId = reader.ReadNetworkId();
            OwnerId = reader.ReadInt32();
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
                reader.ReadSingle());
            Scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            IsActive = reader.ReadBoolean();
            SceneId = reader.ReadInt32();
            EventId = reader.ReadNetworkId();
        }
    }
}