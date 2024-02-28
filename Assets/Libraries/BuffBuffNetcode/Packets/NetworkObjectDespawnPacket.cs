using System.IO;
using BuffBuffNetcode.Interface;
using BuffBuffNetcode.Misc;

namespace BuffBuffNetcode.Packets
{
    public class NetworkObjectDespawnPacket : IPacket
    {
        public NetworkId Id { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
        }
    }
}