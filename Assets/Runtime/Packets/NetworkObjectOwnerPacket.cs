using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    /// Packet used to change the owner of a network object.
    /// </summary>
    public class NetworkObjectOwnerPacket : IPacket
    {
        public NetworkId Id { get; set; }

        public int OwnerId { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(OwnerId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            OwnerId = reader.ReadInt32();
        }
    }
}