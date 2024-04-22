using NetBuff.Interface;

namespace NetBuff.Misc
{
    /// <summary>
    /// Base class for packet listeners
    /// Used to listen for packets and call events when a packet is received
    /// </summary>
    public abstract class PacketListener
    {
        /// <summary>
        /// Call the OnServerReceive event.
        /// Can be used to simulate a server receiving a packet.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="client"></param>
        public abstract void CallOnServerReceive(IPacket packet, int client);

        /// <summary>
        /// Call the OnClientReceive event.
        /// Can be used to simulate a client receiving a packet.
        /// </summary>
        /// <param name="packet"></param>
        public abstract void CallOnClientReceive(IPacket packet);
    }

    /// <summary>
    /// Generic packet listener. Used to listen for packets of a specific type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PacketListener<T> : PacketListener where T : IPacket
    {
        public delegate void ClientReceiveHandler(T packet);

        public delegate void ServerReceiveHandler(T packet, int client);

        public event ServerReceiveHandler OnServerReceive;
        public event ClientReceiveHandler OnClientReceive;

        public override void CallOnServerReceive(IPacket packet, int client)
        {
            OnServerReceive?.Invoke((T)packet, client);
        }

        public override void CallOnClientReceive(IPacket packet)
        {
            OnClientReceive?.Invoke((T)packet);
        }
    }
}