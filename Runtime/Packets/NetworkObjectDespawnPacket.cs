using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
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