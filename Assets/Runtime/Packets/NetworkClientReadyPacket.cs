using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    ///     Packet sent from the client to the server to indicate that the client is ready.
    /// </summary>
    public class NetworkClientReadyPacket : IPacket
    {
        /// <summary>
        ///     The id of the client that is ready.
        /// </summary>
        public int ClientId { get; set; }

        /// <summary>
        ///     Indicates whether the client is ready or not.
        /// </summary>

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