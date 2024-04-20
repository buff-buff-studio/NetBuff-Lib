using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    /// Used by the server to tell the client what their client id is
    /// </summary>
    public class ClientIdPacket : IPacket
    {
        /// <summary>
        /// Represents the client id
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