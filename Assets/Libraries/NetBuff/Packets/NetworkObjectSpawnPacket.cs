using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Packets
{
    public class NetworkObjectSpawnPacket : IPacket
    {
        public NetworkId Id { get; set; }
        public NetworkId PrefabId { get; set; }
        public int OwnerId { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public bool IsActive { get; set; }
        public bool IsRetroactive { get; set; }
        
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
        }
    }
}