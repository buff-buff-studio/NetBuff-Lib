using System;
using NetBuff.Misc;

namespace NetBuff.Discover
{
    /// <summary>
    /// Base class for server discovery. Used to find servers and retrieve their information.
    /// </summary>
    public abstract class ServerDiscoverer
    {
        /// <summary>
        /// Holds the information about a server.
        /// </summary>
        public abstract class ServerInfo
        {
            /// <summary>
            /// The server's name. Normally the host's name.
            /// </summary>
            public string Name { get; set; }
            
            /// <summary>
            /// The number of players currently in the server.
            /// </summary>
            public int Players { get; set; }
            
            /// <summary>
            /// The maximum number of players that can join the server at the same time.
            /// </summary>
            public int MaxPlayers { get; set; }
            
            /// <summary>
            /// The platform the server is running on.
            /// </summary>
            public Platform Platform { get; set; }
        
            /// <summary>
            /// Returns true if the server is password protected.
            /// </summary>
            public bool HasPassword { get; set; }
            
            /// <summary>
            /// Returns the method used to connect to the server (UDP, Bluetooth, etc).
            /// </summary>
            public string Method { get; set; }
            
            /// <summary>
            /// Format the server information to a string.
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return $"{Name}'s game - {Players}/{MaxPlayers} {Platform} {(HasPassword ? "[Password]" : "")}";
            }

            /// <summary>
            /// Join the server.
            /// </summary>
            /// <returns></returns>
            public abstract bool Join();
        }
        
        /// <summary>
        /// Start searching for servers.
        /// For each server found, the onFindServer callback will be called.
        /// After the search is finished, the onFinish callback will be called.
        /// </summary>
        /// <param name="onFindServer"></param>
        /// <param name="onFinish"></param>
        public abstract void Search(Action<ServerInfo> onFindServer, Action onFinish);
        
        /// <summary>
        /// Cancel the search.
        /// When this method is called, the onFinish callback will not be called.
        /// </summary>
        public abstract void Cancel();
    }

    /// <summary>
    /// Base class for server discovery. Used to find servers and retrieve their information.
    /// </summary>
    public abstract class ServerDiscoverer<T> : ServerDiscoverer where T : ServerDiscoverer.ServerInfo
    {
        /// <summary>
        /// Start searching for servers.
        /// For each server found, the onFindServer callback will be called.
        /// After the search is finished, the onFinish callback will be called.
        /// </summary>
        /// <param name="onFindServer"></param>
        /// <param name="onFinish"></param>
        public abstract void Search(Action<T> onFindServer, Action onFinish);
        
        /// <summary>
        /// Start searching for servers.
        /// For each server found, the onFindServer callback will be called.
        /// After the search is finished, the onFinish callback will be called.
        /// </summary>
        /// <param name="onFindServer"></param>
        /// <param name="onFinish"></param>
        public override void Search(Action<ServerInfo> onFindServer, Action onFinish)
        {
            Search(info => onFindServer?.Invoke(info), onFinish);
        }
    }
}