using NetBuff.Misc;

namespace NetBuff.Interface
{
    /// <summary>
    ///     Base interface for all owned packets.
    ///     Owned packets are packets that have an NetworkIdentity owner.
    ///     They will be sent through the network to the owner of the packet automatically.
    ///     Packets are used to send data between the server and the client.
    ///     They are serialized to bytes and deserialized from bytes to be sent over the network.
    /// </summary>
    public interface IOwnedPacket : IPacket
    {
        /// <summary>
        ///     The id of the owner of the packet.
        /// </summary>
        public NetworkId Id { get; set; }
    }
}