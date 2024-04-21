using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    /// Used to sync the active state of a network object over the network
    /// </summary>
    public class NetworkObjectActivePacket : IPacket
    {
        /// <summary>
        /// The id of the network object
        /// </summary>
        public NetworkId Id { get; set; }
        
        /// <summary>
        /// The active state of the network object
        /// </summary>
        public bool IsActive { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(IsActive);
        }
        
        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            IsActive = reader.ReadBoolean();
        }
    }
}