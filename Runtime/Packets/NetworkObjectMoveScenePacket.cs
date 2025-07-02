using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    ///     Packet used to move a network object to a new scene.
    /// </summary>
    public class NetworkObjectMoveScenePacket : IPacket
    {
        /// <summary>
        ///     The network id of the network object.
        /// </summary>
        [InspectorMode(InspectorMode.Object)]
        public NetworkId Id { get; set; }

        /// <summary>
        ///     The scene id to move the network object to.
        /// </summary>
        [InspectorMode(InspectorMode.Scene)]
        public int SceneId { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(SceneId);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
            SceneId = reader.ReadInt32();
        }
    }
}