using NetBuff.Interface;

namespace NetBuff.Misc
{
    public abstract class PacketListener
    {
        public abstract void CallOnServerReceive(IPacket packet, int client);
        
        public abstract void CallOnClientReceive(IPacket packet);
    }
    
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