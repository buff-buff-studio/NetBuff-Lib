using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    [PacketInspectorPriority(PacketInspectorPriority.Normal)]
    public class NetworkBehaviourDataPacket : IPacket
    {
        [NetworkIdInspectorMode(NetworkIdInspectorMode.Object)]
        public NetworkId Id { get; set; }

        [NetworkIdInspectorMode(NetworkIdInspectorMode.Behaviour)]
        public byte BehaviourId { get; set; }

        [NetworkIdInspectorMode(NetworkIdInspectorMode.Data)]
        public byte[] Payload { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(BehaviourId);
            writer.Write(Payload.Length);
            writer.Write(Payload);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
            BehaviourId = reader.ReadByte();
            var length = reader.ReadInt32();
            Payload = reader.ReadBytes(length);
        }
    }
}