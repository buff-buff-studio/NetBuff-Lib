using System;
using System.IO;
using NetBuff.Interface;

namespace NetBuff.Packets
{
    /// <summary>
    ///     Packet used to synchronize the session data of a client.
    /// </summary>
    public class NetworkSessionDataPacket : IPacket
    {
        /// <summary>
        ///     The id of the client.
        /// </summary>
        public int ClientId { get; set; }

        /// <summary>
        ///     The data of the session.
        /// </summary>
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