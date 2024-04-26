using System;
using NetBuff.Interface;

namespace NetBuff
{
    /// <summary>
    ///     A registry for packet types.
    ///     Allows for easy creation of packets by ID.
    /// </summary>
    public static class PacketRegistry
    {
        private static int _nextId;
        private static Type[] _packets = new Type[8];

        /// <summary>
        ///     Registers a packet type.
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
        ///     Registers a packet type.
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
        ///     Clears the registry.
        /// </summary>
        public static void Clear()
        {
            _nextId = 0;
            _packets = new Type[8];
        }

        /// <summary>
        ///     Returns the id of a packet type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static int GetId<T>() where T : IPacket, new()
        {
            return Array.IndexOf(_packets, typeof(T));
        }

        /// <summary>
        ///     Returns the id of a packet.
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public static int GetId(IPacket packet)
        {
            return Array.IndexOf(_packets, packet.GetType());
        }

        /// <summary>
        ///     Creates a packet by its id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static IPacket CreatePacket(int id)
        {
            if (id < 0 || id >= _packets.Length)
                return null;

            return (IPacket)Activator.CreateInstance(_packets[id]);
        }
    }
}