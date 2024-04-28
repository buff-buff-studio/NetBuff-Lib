using System;
using System.Collections.Generic;
using NetBuff.Interface;

namespace NetBuff.Misc
{
    /// <summary>
    ///     Base class for packet listeners
    ///     Used to listen for packets and call events when a packet is received
    /// </summary>
    public abstract class PacketListener
    {
        private static readonly Dictionary<Type, PacketListener> _PacketListeners = new();
        
        #region Listeners
        /// <summary>
        ///     Returns the packet listener for the given packet type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static PacketListener<T> GetPacketListener<T>() where T : IPacket
        {
            if (_PacketListeners.TryGetValue(typeof(T), out var listener))
                return (PacketListener<T>)listener;

            listener = new PacketListener<T>();
            _PacketListeners.Add(typeof(T), listener);

            return (PacketListener<T>)listener;
        }

        /// <summary>
        ///     Returns the packet listener for the given packet type.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static PacketListener GetPacketListener(Type type)
        {
            if (_PacketListeners.TryGetValue(type, out var listener))
                return listener;

            listener = (PacketListener)Activator.CreateInstance(typeof(PacketListener<>).MakeGenericType(type));
            _PacketListeners.Add(type, listener);

            return listener;
        }
        #endregion

        
        /// <summary>
        ///     Call the OnServerReceive event.
        ///     Can be used to simulate a server receiving a packet.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="client"></param>
        public abstract bool CallOnServerReceive(IPacket packet, int client);

        /// <summary>
        ///     Call the OnClientReceive event.
        ///     Can be used to simulate a client receiving a packet.
        /// </summary>
        /// <param name="packet"></param>
        public abstract bool CallOnClientReceive(IPacket packet);
    }

    /// <summary>
    ///     Generic packet listener. Used to listen for packets of a specific type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PacketListener<T> : PacketListener where T : IPacket
    {
        private readonly List<Func<T, int, bool>> _onServerReceive = new();
        private readonly List<Func<T, bool>> _onClientReceive = new();
        
        public void AddServerListener(Func<T, int, bool> callback)
        {
            _onServerReceive.Add(callback);
        }
        
        public void AddClientListener(Func<T, bool> callback)
        {
            _onClientReceive.Add(callback);
        }
        
        public void RemoveServerListener(Func<T, int, bool> callback)
        {
            _onServerReceive.Remove(callback);
        }
        
        public void RemoveClientListener(Func<T, bool> callback)
        {
            _onClientReceive.Remove(callback);
        }

        public override bool CallOnServerReceive(IPacket packet, int client)
        {
            foreach (var callback in _onServerReceive)
            {
                if (callback.Invoke((T)packet, client))
                    return true;
            }
            
            return false;
        }

        public override bool CallOnClientReceive(IPacket packet)
        {
            foreach (var callback in _onClientReceive)
            {
                if (callback.Invoke((T)packet))
                    return true;
            }
            
            return false;
        }
    }
}