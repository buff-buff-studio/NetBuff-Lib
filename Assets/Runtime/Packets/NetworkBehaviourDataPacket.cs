using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    ///     Packet used to synchronize data of a network behaviour.
    /// </summary>
    public class NetworkBehaviourDataPacket : IPacket
    {
        /// <summary>
        ///     The network id of the network object.
        /// </summary>
        [InspectorMode(InspectorMode.Object)]
        public NetworkId Id { get; set; }

        /// <summary>
        ///     The behaviour id of the network behaviour (index in the network object).
        /// </summary>
        [InspectorMode(InspectorMode.Behaviour)]
        public byte BehaviourId { get; set; }

        /// <summary>
        ///     The payload of the network behaviour data.
        /// </summary>
        [InspectorMode(InspectorMode.Data)]
        public byte[] Payload { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(BehaviourId);
            writer.Write(Payload.Length);
            writer.Write(Payload);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            BehaviourId = reader.ReadByte();
            var length = reader.ReadInt32();
            Payload = reader.ReadBytes(length);
        }
    }
}