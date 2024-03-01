using System.IO;

namespace NetBuff.Interface
{
    public interface IPacket
    {
        void Serialize(BinaryWriter writer);
        void Deserialize(BinaryReader reader);
    }
}