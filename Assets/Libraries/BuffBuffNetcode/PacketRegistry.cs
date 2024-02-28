using System;
using BuffBuffNetcode.Interface;

namespace BuffBuffNetcode
{
    /// <summary>
    /// Class to register all packets used via the network
    /// </summary>
    public static class PacketRegistry
    {
        private static int _nextId;
        private static Type[] _packets = new Type[8];
        
        /// <summary>
        /// Register a packet type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static void RegisterPacket<T>() where T : IPacket, new()
        {
            if (_nextId >= _packets.Length)
            {
                var arr = new Type[_packets.Length * 2];
                Array.Copy(_packets, arr, _packets.Length);
                _packets = arr;
            }
            
            _packets[_nextId] = typeof(T);
            _nextId++;
        }
        
        /// <summary>
        /// Register a packet type
        /// </summary>
        /// <param name="type"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void RegisterPacket(Type type)
        {
            if (!typeof(IPacket).IsAssignableFrom(type))
                throw new ArgumentException("Type must implement IPacket");
            
            if (_nextId >= _packets.Length)
            {
                var arr = new Type[_packets.Length * 2];
                Array.Copy(_packets, arr, _packets.Length);
                _packets = arr;
            }
            
            _packets[_nextId] = type;
            _nextId++;
        }
        
        
        /// <summary>
        /// Clear all registered packets
        /// </summary>
        public static void Clear()
        {
            _nextId = 0;
            _packets = new Type[8];
        }
        
        /// <summary>
        /// Get the id for a packet type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static int GetId<T>() where T : IPacket, new()
        {
            return Array.IndexOf(_packets, typeof(T));
        }
        
        /// <summary>
        /// Get the id for a packet
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static int GetId(IPacket packet)
        {
            return Array.IndexOf(_packets, packet.GetType());
        }
        
        /// <summary>
        /// Create a new packet from its type id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static IPacket CreatePacket(int id)
        {
            if (id < 0 || id >= _packets.Length)
                return null;
            
            return (IPacket) Activator.CreateInstance(_packets[id]);
        }
    }
}