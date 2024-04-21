using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    /// Used to sync unloading a scene over the network
    /// </summary>
    public class NetworkUnloadScenePacket : IPacket
    {
        /// <summary>
        /// Represents the name of the scene to unload
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