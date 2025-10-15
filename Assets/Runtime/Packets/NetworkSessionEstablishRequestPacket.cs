using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
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