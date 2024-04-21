using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    /// Used to sync values of a network object over the network
    /// </summary>
    public class NetworkValuesPacket : IPacket
    {
        /// <summary>
        /// The id of the network object
        /// </summary>
        public NetworkId Id { get; set; }
        
        /// <summary>
        /// The index of the behaviour
        /// </summary>
        public byte BehaviourId { get; set; }
        
        /// <summary>
        /// Current payload of the behaviour data
        /// The NetworkValues and custom serializable data will be packed into this payload
        /// </summary>
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