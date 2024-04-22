using System.IO;

namespace NetBuff.Interface
{
    /// <summary>
    /// Base interface for all packets.
    /// Packets are used to send data between the server and the client.
    /// They are serialized to bytes and deserialized from bytes to be sent over the network.
    /// </summary>
    public interface IPacket
    {
        /// <summary>
        /// Serializes the packet to a binary writer.
        /// </summary>
        /// <param name="writer"></param>
        void Serialize(BinaryWriter writer);

        /// <summary>
        /// Deserializes the packet from a binary reader.
        /// </summary>
        /// <param name="reader"></param>
        void Deserialize(BinaryReader reader);
    }
}