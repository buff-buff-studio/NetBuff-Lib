using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    public class NetworkUnloadScenePacket : IPacket
    {
        public string SceneName { get; set; }
        
        public NetworkId EventId { get; set; } = NetworkId.Empty;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(SceneName);
            writer.Write(EventId);
        }

        public void Deserialize(BinaryReader reader)
        {
            SceneName = reader.ReadString();
            EventId = reader.ReadNetworkId();   
        }
    }
}