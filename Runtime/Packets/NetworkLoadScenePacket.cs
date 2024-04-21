using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    /// Used to sync loading a scene over the network
    /// </summary>
    public class NetworkLoadScenePacket : IPacket
    {
        /// <summary>
        /// Represents the name of the scene to load
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
