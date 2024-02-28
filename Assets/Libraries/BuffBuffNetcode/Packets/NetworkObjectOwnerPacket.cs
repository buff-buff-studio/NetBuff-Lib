using System.IO;
using BuffBuffNetcode.Interface;
using BuffBuffNetcode.Misc;

namespace BuffBuffNetcode.Packets
{
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