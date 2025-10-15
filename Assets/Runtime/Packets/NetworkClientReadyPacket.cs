using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    public class NetworkClientReadyPacket : IPacket
    {
        public int ClientId { get; set; }


        public bool IsReady { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ClientId);
            writer.Write(IsReady);
        }

        public void Deserialize(BinaryReader reader)
        {
            ClientId = reader.ReadInt32();
            IsReady = reader.ReadBoolean();
        }
    }
}