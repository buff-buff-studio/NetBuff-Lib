using System;
using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    public class NetworkSessionDataPacket : IPacket
    {
        public int ClientId { get; set; }

        [NetworkIdInspectorMode(NetworkIdInspectorMode.Data)]
        public ArraySegment<byte> Data { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ClientId);
            writer.Write(Data.Count);
            writer.Write(Data.Array!, Data.Offset, Data.Count);
        }

        public void Deserialize(BinaryReader reader)
        {
            ClientId = reader.ReadInt32();
            var count = reader.ReadInt32();
            var buffer = new byte[count];
            // ReSharper disable once MustUseReturnValue
            reader.Read(buffer, 0, count);
            Data = new ArraySegment<byte>(buffer);
        }
    }
}