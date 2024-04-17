using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    public class NetworkLoadScenePacket : IPacket
    {
        public int LoadSceneMode { get; set; }
        public string SceneName { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(SceneName);
            writer.Write(LoadSceneMode);
        }

        public void Deserialize(BinaryReader reader)
        {
            SceneName = reader.ReadString();
            LoadSceneMode = reader.ReadInt32();
        }
    }
    
    public class NetworkUnloadScenePacket : IPacket
    {
        public string SceneName { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(SceneName);
        }

        public void Deserialize(BinaryReader reader)
        {
            reader.ReadString();
        }
    }
}