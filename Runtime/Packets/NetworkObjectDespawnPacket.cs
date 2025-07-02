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
        [InspectorMode(InspectorMode.Object)]
        public NetworkId Id { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
        }
    }
}