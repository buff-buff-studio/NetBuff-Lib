using System;
using NetBuff.Misc;

namespace NetBuff.Discover
{
    public abstract class ServerDiscover<T> where T : ServerDiscover<T>.GameInfo
    {
        public class GameInfo
        {
            public string Name { get; set; }
            
            public int Players { get; set; }
            
            public int MaxPlayers { get; set; }
            
            public Platform Platform { get; set; }
            
            public bool HasPassword { get; set; }
            
            public string Method { get; set; }
    
            public override string ToString()
            {
                return $"{Name}'s game - Players: {Players}/{MaxPlayers}, Platform: {Platform}, HasPassword: {HasPassword}";
            }
        }
        
        public abstract void Search(Action<T> onFindServer, Action onFinish);
        
        public abstract void Cancel();
    }
}