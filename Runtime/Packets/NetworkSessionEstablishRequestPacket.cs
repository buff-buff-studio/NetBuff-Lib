using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    ///     Base packet for all session establish request packets.
    /// </summary>
    public class NetworkSessionEstablishRequestPacket : IPacket
    {
        public virtual void Serialize(BinaryWriter writer)
        {
        }


        public virtual void Deserialize(BinaryReader reader)
        {
        }
    }
}