using System;
using NetBuff.Misc;

namespace NetBuff.Discover
{
    public abstract class ServerDiscoverer
    {
        public abstract void Search(Action<ServerInfo> onFindServer, Action onFinish);

        public abstract void Cancel();

        public abstract class ServerInfo
        {
            public string Name { get; set; }

            public int Players { get; set; }

            public int MaxPlayers { get; set; }

            public Platform Platform { get; set; }

            public bool HasPassword { get; set; }

            public string Method { get; set; }

            public override string ToString()
            {
                return $"{Name}'s game - {Players}/{MaxPlayers} {Platform} {(HasPassword ? "[Password]" : "")}";
            }

            public abstract bool Join();
        }
    }

    public abstract class ServerDiscoverer<T> : ServerDiscoverer where T : ServerDiscoverer.ServerInfo
    {
        public abstract void Search(Action<T> onFindServer, Action onFinish);

        public override void Search(Action<ServerInfo> onFindServer, Action onFinish)
        {
            Search(info => onFindServer?.Invoke(info), onFinish);
        }
    }
}