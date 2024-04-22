using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    /// Packet sent from the server to the client to inform the client to unload a scene.
    /// </summary>
    public class NetworkUnloadScenePacket : IPacket
    {
        /// <summary>
        /// The name of the scene to unload.
        /// </summary>
        public string SceneName { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(SceneName);
        }

        public void Deserialize(BinaryReader reader)
        {
            SceneName = reader.ReadString();
        }
    }
}