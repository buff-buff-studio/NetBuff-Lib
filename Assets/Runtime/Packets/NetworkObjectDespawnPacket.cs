using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    ///     Packet used to despawn a network object.
    /// </summary>
    public class NetworkObjectDespawnPacket : IPacket
    {
        /// <summary>
        ///     The network id of the network object.
        /// </summary>
        public NetworkId Id { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
        }
    }
}