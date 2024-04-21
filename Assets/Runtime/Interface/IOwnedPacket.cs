using NetBuff.Misc;

namespace NetBuff.Interface
{
    public interface IOwnedPacket : IPacket
    {
        public NetworkId Id { get; set; }
    }
}