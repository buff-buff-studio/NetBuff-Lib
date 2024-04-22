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
        void Serialize(BinaryWriter writer);

        void Deserialize(BinaryReader reader);
    }
}