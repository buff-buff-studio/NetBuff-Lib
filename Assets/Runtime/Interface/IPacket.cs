using System.IO;

namespace NetBuff.Interface
{
    /// <summary>
    /// Base class for all packets
    /// </summary>
    public interface IPacket
    {
        /// <summary>
        /// Serializes all the data of the packet
        /// </summary>
        /// <param name="writer"></param>
        void Serialize(BinaryWriter writer);
        
        /// <summary>
        /// Deserializes all the data of the packet
        /// </summary>
        /// <param name="reader"></param>
        void Deserialize(BinaryReader reader);
    }
}