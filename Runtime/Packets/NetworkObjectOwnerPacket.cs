using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    public class NetworkObjectOwnerPacket : IPacket
    {
        [NetworkIdInspectorMode(NetworkIdInspectorMode.Object)]
        public NetworkId Id { get; set; }

        [NetworkIdInspectorMode(NetworkIdInspectorMode.Owner)]
        public int OwnerId { get; set; }

        public NetworkId EventId { get; set; } = NetworkId.Empty;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(OwnerId);
            writer.Write(EventId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
            OwnerId = reader.ReadInt32();
            EventId = reader.ReadNetworkId();
        }
    }
}