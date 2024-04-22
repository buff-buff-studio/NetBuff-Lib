using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    /// Packet used to confirm that the client has synchronized the server state.
    /// This allows the server to confirm the client connection.
    /// </summary>
    public class NetworkPreExistingResponsePacket : IPacket
    {
        public void Serialize(BinaryWriter writer)
        {
        }

        public void Deserialize(BinaryReader reader)
        {
        }
    }
}