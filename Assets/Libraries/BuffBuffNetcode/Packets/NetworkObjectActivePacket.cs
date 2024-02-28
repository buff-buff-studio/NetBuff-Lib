using System.IO;
using BuffBuffNetcode.Interface;
using BuffBuffNetcode.Misc;

namespace BuffBuffNetcode.Packets
{
    public class NetworkObjectActivePacket : IPacket
    {
        public NetworkId Id { get; set; }
        public bool IsActive { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(IsActive);
        }
        
        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            IsActive = reader.ReadBoolean();
        }
    }
}