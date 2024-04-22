using System;
using NetBuff.Interface;

namespace NetBuff
{
    public static class PacketRegistry
    {
        private static int _nextId;
        private static Type[] _packets = new Type[8];

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


        public static void Clear()
        {
            _nextId = 0;
            _packets = new Type[8];
        }

        public static int GetId<T>() where T : IPacket, new()
        {
            return Array.IndexOf(_packets, typeof(T));
        }

        public static int GetId(IPacket packet)
        {
            return Array.IndexOf(_packets, packet.GetType());
        }

        public static IPacket CreatePacket(int id)
        {
            if (id < 0 || id >= _packets.Length)
                return null;

            return (IPacket)Activator.CreateInstance(_packets[id]);
        }
    }
}