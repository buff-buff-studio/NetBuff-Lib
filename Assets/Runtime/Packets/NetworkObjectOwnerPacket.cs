using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    /// Used to sync the owner of a network object over the network
    /// </summary>
    public class NetworkObjectOwnerPacket : IPacket
    {
        /// <summary>
        /// The id of the network object
        /// </summary>
        public NetworkId Id { get; set; }
        
        /// <summary>
        /// The id of the owner of the network object
        /// If the owner id is -1, the object is owned by the server
        /// </summary>
        public int OwnerId { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(OwnerId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            OwnerId = reader.ReadInt32();
        }
    }
}