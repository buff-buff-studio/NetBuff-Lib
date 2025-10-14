using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NetBuff.Misc
{   
    public abstract class NetworkEvent
    {
        private static readonly Dictionary<NetworkId, NetworkEvent> _Events = new();
        
        public bool IsValid { get; private set; }

        public abstract void Invoke(object result);

        public static NetworkId Register(NetworkEvent networkEvent)
        {
            if (networkEvent == null)
                throw new ArgumentNullException(nameof(networkEvent));
            
            if (networkEvent.IsValid)
                throw new InvalidOperationException("NetworkEvent is already registered.");

            var id = NetworkId.New();
            while (_Events.ContainsKey(id))
                id = NetworkId.New();
            
            _Events[id] = networkEvent;
            networkEvent.IsValid = true;
            return id;
        }
        
        public static void Unregister(NetworkId id)
        {
            if (!_Events.TryGetValue(id, out var @event)) 
                return;
            
            @event.IsValid = false;
            _Events.Remove(id);
        }

        public static bool TryGetEvent(NetworkId id, out NetworkEvent networkEvent)
        {
            return _Events.TryGetValue(id, out networkEvent);
        }
        
        public static bool InvokeSafely(NetworkId id, object result, bool unregister = true)
        {
            if (TryGetEvent(id, out var networkEvent))
            {
                networkEvent.Invoke(result);
                if (unregister)
                    _Events.Remove(id);
                return true;
            }
            
            return false;
        }

        public static void ClearEvents()
        {
            _Events.Clear();
        }
    }

    public class NetworkEvent<T> : NetworkEvent
    {
        private Action<T> _then;
        public int CallCount { get; private set; }
        
        public void Then(Action<T> then)
        {
            _then += then;
        }

        public override void Invoke(object result)
        {
            CallCount++;
            _then?.Invoke((T)result);
        }
        
        public async Task AsTask()
        {
            while(IsValid && CallCount == 0)
                await Awaitable.NextFrameAsync();
        }
        
        public static implicit operator Task(NetworkEvent<T> networkEvent)
        {
            return networkEvent.AsTask();
        }
    }
}