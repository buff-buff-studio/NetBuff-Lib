using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    ///     Packet used to change the active state of a network object.
    /// </summary>
    public class NetworkObjectActivePacket : IPacket
    {
        /// <summary>
        ///     The network id of the network object.
        /// </summary>
        [InspectorMode(InspectorMode.Object)]
        public NetworkId Id { get; set; }

        /// <summary>
        ///     The active state of the network object.
        /// </summary>
        public bool IsActive { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(IsActive);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
            IsActive = reader.ReadBoolean();
        }
    }
}