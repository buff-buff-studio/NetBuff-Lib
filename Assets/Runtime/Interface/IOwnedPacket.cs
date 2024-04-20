using NetBuff.Misc;

namespace NetBuff.Interface
{
    /// <summary>
    /// Base interface for packets that are owned by a given NetworkIdentity
    /// </summary>
    public interface IOwnedPacket : IPacket
    {
        /// <summary>
        /// The network id of the NetworkIdentity that owns this packet
        /// </summary>
        public NetworkId Id { get; set; }
    }
}