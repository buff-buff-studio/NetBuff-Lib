using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff.Packets
{
    public class NetworkValuesPacket : IPacket
    {
        public NetworkId IdentityId { get; set; }
        public NetworkId BehaviourId { get; set; }
        public byte[] Payload { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            IdentityId.Serialize(writer);
            BehaviourId.Serialize(writer);
            writer.Write(Payload.Length);
            writer.Write(Payload);
        }

        public void Deserialize(BinaryReader reader)
        {
            IdentityId = NetworkId.Read(reader);
            BehaviourId = NetworkId.Read(reader);
            var length = reader.ReadInt32();
            Payload = reader.ReadBytes(length);
        }
    }
}