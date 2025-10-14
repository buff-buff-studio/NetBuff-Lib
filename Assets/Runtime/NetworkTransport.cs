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
        public enum ConnectionEndMode
        {
            Shutdown,

            InternalError,
        }

        public enum EnvironmentType
        {
            None,

            Server,

            Host,

            Client
        }

        #region Utils Methods
        public abstract ServerDiscoverer GetServerDiscoverer();
        #endregion

        #region Helper Properties
        public EnvironmentType Type { get; protected set; } = EnvironmentType.None;

        [ClientOnly]
        public IConnectionInfo ClientConnectionInfo { get; protected set; }
        #endregion

        #region Callbacks
        public Action<int, IPacket> OnServerPacketReceived { get; set; }

        public Action<IPacket> OnClientPacketReceived { get; set; }

        public Action<int> OnClientConnected { get; set; }

        public Action<int, string> OnClientDisconnected { get; set; }

        public Action OnConnect { get; set; }

        public Action<ConnectionEndMode, string> OnDisconnect { get; set; }

        public Action OnServerStart { get; set; }

        public Action<ConnectionEndMode, string> OnServerStop { get; set; }
        #endregion

        #region Management Methods
        public abstract void StartHost(int magicNumber);

        public abstract void StartServer();

        public abstract void StartClient(int magicNumber);

        public abstract void Close();
        #endregion

        #region Server Client Methods
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
        public abstract void ClientSendPacket(IPacket packet, bool reliable = false);

        [ServerOnly]
        public abstract void ServerSendPacket(IPacket packet, int target = -1, bool reliable = false);

        [ServerOnly]
        public void BroadcastServerPacket(IPacket packet, bool reliable = false)
        {
            ServerSendPacket(packet, -1, reliable);
        }
        #endregion
    }
}