using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    public class NetworkObjectDespawnPacket : IPacket
    {
        [NetworkIdInspectorMode(NetworkIdInspectorMode.Object)]
        public NetworkId Id { get; set; }

        public NetworkId EventId { get; set; } = NetworkId.Empty;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(EventId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
            EventId = reader.ReadNetworkId();
        }
    }
}