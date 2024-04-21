using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    /// Used by the client to tell the server that the client has finished syncing the network state
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