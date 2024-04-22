using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    public class NetworkClientIdPacket : IPacket
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