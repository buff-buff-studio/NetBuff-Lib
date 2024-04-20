using System;
using NetBuff.Misc;

namespace NetBuff.Discover
{
    /// <summary>
    /// Base class for server discovery. Used to find available servers
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ServerDiscover<T> where T : ServerDiscover<T>.GameInfo
    {
        /// <summary>
        /// Holds a game server info
        /// </summary>
        public class GameInfo
        {
            /// <summary>
            /// Current game server name
            /// By default it's equals to the host name
            /// </summary>
            public string Name { get; set; }
            
            /// <summary>
            /// Current online player count
            /// </summary>
            public int Players { get; set; }
            
            /// <summary>
            /// Represents the maximum player count
            /// </summary>
            public int MaxPlayers { get; set; }
            
            /// <summary>
            /// Current host platform
            /// </summary>
            public Platform Platform { get; set; }
            
            /// <summary>
            /// Represents if the server has a password check
            /// </summary>
            public bool HasPassword { get; set; }
            
            /// <summary>
            /// Current server hosting method
            /// Can be UDP, Bluetooth, etc...
            /// </summary>
            public string Method { get; set; }
    
            public override string ToString()
            {
                return $"{Name}'s game - Players: {Players}/{MaxPlayers}, Platform: {Platform}, HasPassword: {HasPassword}";
            }
        }
        
        /// <summary>
        /// Perform a search for available servers
        /// </summary>
        /// <param name="onFindServer"></param>
        /// <param name="onFinish"></param>
        public abstract void Search(Action<T> onFindServer, Action onFinish);
        
        
        /// <summary>
        /// Cancel the current search
        /// </summary>
        public abstract void Cancel();
    }
}