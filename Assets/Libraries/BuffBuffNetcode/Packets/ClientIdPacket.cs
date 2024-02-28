using System.IO;
using BuffBuffNetcode.Interface;

namespace BuffBuffNetcode.Packets
{
    public class ClientIdPacket : IPacket
    {
        public int ClientId { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ClientId);
        }

        public void Deserialize(BinaryReader reader)
        {
            ClientId = reader.ReadInt32();
        }
    }
}