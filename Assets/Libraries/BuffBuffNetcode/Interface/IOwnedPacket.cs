using BuffBuffNetcode.Misc;

namespace BuffBuffNetcode.Interface
{
    public interface IOwnedPacket : IPacket
    {
        public NetworkId Id { get; set; }
    }
}