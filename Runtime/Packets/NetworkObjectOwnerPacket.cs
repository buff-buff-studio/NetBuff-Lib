using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    ///     Packet used to change the owner of a network object.
    /// </summary>
    public class NetworkObjectOwnerPacket : IPacket
    {
        /// <summary>
        ///     The network id of the network object.
        /// </summary>
        [InspectorMode(InspectorMode.Object)]
        public NetworkId Id { get; set; }

        /// <summary>
        ///     The owner id of the network object.
        /// </summary>
        [InspectorMode(InspectorMode.Owner)]
        public int OwnerId { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(OwnerId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
            OwnerId = reader.ReadInt32();
        }
    }
}