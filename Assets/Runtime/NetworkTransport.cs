using System;
using System.Collections.Generic;
using NetBuff.Interface;
using UnityEngine;

namespace NetBuff
{
    /// <summary>
    /// Base class for network transport
    /// </summary>
    public abstract class NetworkTransport : MonoBehaviour
    {
        public enum EndType
        {
            None,
            Server, 
            Host,
            Client
        }
        
        public EndType Type { get; protected set; } = EndType.None;
        public IConnectionInfo ClientConnectionInfo { get; protected set; }

        public string Name { get => name; set => name = value; }

        [SerializeField]
        private new string name = "server";

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

        #region ManagementMethods
        public abstract void StartHost();
        public abstract void StartServer();
        public abstract void StartClient();
        public abstract void Close();
        #endregion

        #region Client Methods
        public abstract IClientConnectionInfo GetClientInfo(int id);
        public abstract int GetClientCount();
        public abstract IEnumerable<IClientConnectionInfo> GetClients();
        #endregion

        #region Lifecycle
        public abstract void ClientDisconnect(string reason);
        public abstract void ServerDisconnect(int id, string reason);
        public abstract void SendClientPacket(IPacket packet, bool reliable = false);
        public abstract void SendServerPacket(IPacket packet, int target = -1, bool reliable = false);

        public void BroadcastServerPacket(IPacket packet, bool reliable = false)
        {
            SendServerPacket(packet, -1, reliable);
        }
        #endregion
    }
}