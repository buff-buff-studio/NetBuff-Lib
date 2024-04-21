using System;
using System.Collections.Generic;
using NetBuff.Discover;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff
{
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/transports")]
    public abstract class NetworkTransport : MonoBehaviour
    {
        public enum EndType
        {
            None,
            Server, 
            Host,
            Client
        }

        #region Inspector Fields
        [SerializeField]
        protected new string name = "server";
        #endregion
        
        #region Helper Properties
        public EndType Type { get; protected set; } = EndType.None;
        
        public IConnectionInfo ClientConnectionInfo { get; protected set; }
        
        public string Name { get => name; set => name = value; }
        #endregion

        #region Callbacks
        public Action<int, IPacket> OnServerPacketReceived { get; set; }
        public Action<IPacket> OnClientPacketReceived { get; set; }
        public Action<int> OnClientConnected { get; set; }
        public Action<int, string> OnClientDisconnected { get; set; }
        public Action OnConnect { get; set; }
        public Action<string> OnDisconnect { get; set; }
        
        public Action OnServerStart { get; set; }
        
        public Action OnServerStop { get; set; }
        #endregion

        #region Utils Methods
        public abstract ServerDiscover GetServerDiscoverer();
        #endregion

        #region Management Methods
        public abstract void StartHost(int magicNumber);
        
        public abstract void StartServer();
        
        public abstract void StartClient(int magicNumber);
        
        public abstract void Close();
        #endregion

        #region Client Methods
        [ServerOnly]
        public abstract IClientConnectionInfo GetClientInfo(int id);
        
        [ServerOnly]
        public abstract int GetClientCount();
        
        [ServerOnly]
        public abstract IEnumerable<IClientConnectionInfo> GetClients();
        #endregion

        #region Lifecycle
        [ClientOnly]
        public abstract void ClientDisconnect(string reason);
        
        [ServerOnly]
        public abstract void ServerDisconnect(int id, string reason);
        
        [ClientOnly]
        public abstract void SendClientPacket(IPacket packet, bool reliable = false);
        
        [ServerOnly]
        public abstract void SendServerPacket(IPacket packet, int target = -1, bool reliable = false);
        
        [ServerOnly]
        public void BroadcastServerPacket(IPacket packet, bool reliable = false)
        {
            SendServerPacket(packet, -1, reliable);
        }
        #endregion
    }
}