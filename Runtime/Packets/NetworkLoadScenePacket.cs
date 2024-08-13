using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    /// <summary>
    ///     Packet sent from the server to the client to inform the client to load a scene.
    /// </summary>
    public class NetworkLoadScenePacket : IPacket
    {
        /// <summary>
        ///     The name of the scene to load.
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