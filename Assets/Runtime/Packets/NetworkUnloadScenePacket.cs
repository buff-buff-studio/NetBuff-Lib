using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    public class NetworkUnloadScenePacket : IPacket
    {
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