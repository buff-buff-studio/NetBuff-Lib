using System;
using System.IO;
using NetBuff.Interface;
using NetBuff.Misc;

namespace NetBuff.Packets
{
    [PacketInspectorPriority(PacketInspectorPriority.Low)]
    public class NetworkTransformPacket : IOwnedPacket
    {
        public float[] Components { get; set; } = Array.Empty<float>();

        public short Flag { get; set; }

        [NetworkIdInspectorMode(NetworkIdInspectorMode.Object)]
        public NetworkId Id { get; set; }
        
        public byte BehaviourId { get; set; }
        
        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Flag);
            writer.Write(BehaviourId);

            writer.Write((byte)Components.Length);
            foreach (var t in Components)
                writer.Write(t);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadNetworkId();
            Flag = reader.ReadInt16();
            BehaviourId = reader.ReadByte();

            var count = reader.ReadByte();
            Components = new float[count];
            for (var i = 0; i < count; i++)
                Components[i] = reader.ReadSingle();
        }
    }
}