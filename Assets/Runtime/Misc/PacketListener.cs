using NetBuff.Interface;

namespace NetBuff.Misc
{
    /// <summary>
    /// Used to listen for packets on the server and client
    /// </summary>
    public abstract class PacketListener
    {
        /// <summary>
        /// Used internally to call the OnServerReceive event
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="client"></param>
        public abstract void CallOnServerReceive(IPacket packet, int client);
        
        /// <summary>
        /// Used internally to call the OnClientReceive event
        /// </summary>
        /// <param name="packet"></param>
        public abstract void CallOnClientReceive(IPacket packet);
    }
    
    /// <summary>
    /// Used to listen for packets on the server and client
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PacketListener<T> : PacketListener where T : IPacket
    {
        public delegate void ServerReceiveHandler(T packet, int client);
        public event ServerReceiveHandler OnServerReceive;

        public delegate void ClientReceiveHandler(T packet);
        public event ClientReceiveHandler OnClientReceive;

        public override void CallOnServerReceive(IPacket packet, int client)
        {
            OnServerReceive?.Invoke((T) packet, client);
        }
        
        public override void CallOnClientReceive(IPacket packet)
        {
            OnClientReceive?.Invoke((T) packet);
        }
    }
}