using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    /// Packet sent from the server to the client to inform the client of its network id.
    /// </summary>
    public class NetworkClientIdPacket : IPacket
    {
        /// <summary>
        /// The client id assigned to the client by the server.
        /// </summary>
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