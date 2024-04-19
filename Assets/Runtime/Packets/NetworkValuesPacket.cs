using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    public class NetworkValuesPacket : IPacket
    {
        public NetworkId Id { get; set; }
        public byte BehaviourId { get; set; }
        public byte[] Payload { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(BehaviourId);
            writer.Write(Payload.Length);
            writer.Write(Payload);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            BehaviourId = reader.ReadByte();
            var length = reader.ReadInt32();
            Payload = reader.ReadBytes(length);
        }
    }
}