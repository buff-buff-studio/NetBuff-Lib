using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    /// Used to sync the change the scene of a network object over the network
    /// </summary>
    public class NetworkObjectMoveScenePacket : IPacket
    {
        /// <summary>
        /// The id of the network object
        /// </summary>
        public NetworkId Id { get; set; }
        
        /// <summary>
        /// The id of the target scene
        /// </summary>
        public int SceneId { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            Id.Serialize(writer);
            writer.Write(SceneId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = NetworkId.Read(reader);
            SceneId = reader.ReadInt32();
        }
    }
}