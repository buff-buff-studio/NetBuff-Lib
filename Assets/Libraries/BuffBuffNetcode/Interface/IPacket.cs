using System.IO;

namespace BuffBuffNetcode.Interface
{
    public interface IPacket
    {
        void Serialize(BinaryWriter writer);
        void Deserialize(BinaryReader reader);
    }
}