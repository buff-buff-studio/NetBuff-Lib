using System;
using NetBuff.Misc;

namespace NetBuff.Discover
{
    public abstract class ServerDiscover
    {
        public abstract class GameInfo
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
        
        public abstract void Search(Action<GameInfo> onFindServer, Action onFinish);
    }
    
    public abstract class ServerDiscover<T> : ServerDiscover where T : ServerDiscover.GameInfo
    {
        public abstract void Search(Action<T> onFindServer, Action onFinish);
        public abstract void Cancel();

        public override void Search(Action<GameInfo> onFindServer, Action onFinish)
        {
            Search(info => onFindServer?.Invoke(info), onFinish);
        }
    }
}