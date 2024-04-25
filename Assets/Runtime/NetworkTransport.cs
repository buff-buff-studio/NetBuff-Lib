using System;
using System.Collections.Generic;
using NetBuff.Discover;
using NetBuff.Interface;
using NetBuff.Misc;
using UnityEngine;

namespace NetBuff
{
    /// <summary>
    ///     Base class for network transport.
    ///     Responsible for internally managing the connection between the server and clients.
    ///     Holds the connection information of clients and provides methods for sending and receiving packets.
    /// </summary>
    [HelpURL("https://buff-buff-studio.github.io/NetBuff-Lib-Docs/transports")]
    public abstract class NetworkTransport : MonoBehaviour
    {
        /// <summary>
        ///     Enum for the type of environment.
        /// </summary>
        public enum EnvironmentType
        {
            /// <summary>
            ///     Network environment is not set.
            /// </summary>
            None,

            /// <summary>
            ///     Network environment is server.
            /// </summary>
            Server,

            /// <summary>
            ///     Network environment is host (server and client).
            /// </summary>
            Host,

            /// <summary>
            ///     Network environment is client.
            /// </summary>
            Client
        }

        #region Utils Methods
        /// <summary>
        ///     Returns a new discoverer that will be used to find available servers.
        /// </summary>
        /// <returns></returns>
        public abstract ServerDiscoverer GetServerDiscoverer();
        #endregion

        #region Helper Properties
        /// <summary>
        ///     The current type of the network environment.
        /// </summary>
        public EnvironmentType Type { get; protected set; } = EnvironmentType.None;

        /// <summary>
        ///     The local client connection to the server information.
        /// </summary>
        [ClientOnly]
        public IConnectionInfo ClientConnectionInfo { get; protected set; }
        #endregion

        #region Callbacks
        /// <summary>
        ///     The callback that will be called when a packet is received by the server.
        /// </summary>
        public Action<int, IPacket> OnServerPacketReceived { get; set; }

        /// <summary>
        ///     The callback that will be called when a packet is received by the client.
        /// </summary>
        public Action<IPacket> OnClientPacketReceived { get; set; }

        /// <summary>
        ///     The callback that will be called when a client is connected to the server.
        /// </summary>
        public Action<int> OnClientConnected { get; set; }

        /// <summary>
        ///     The callback that will be called when a client is disconnected from the server.
        /// </summary>
        public Action<int, string> OnClientDisconnected { get; set; }

        /// <summary>
        ///     The callback that will be called when the client connects to the server.
        /// </summary>
        public Action OnConnect { get; set; }

        /// <summary>
        ///     The callback that will be called when the client disconnects from the server.
        /// </summary>
        public Action<string> OnDisconnect { get; set; }

        /// <summary>
        ///     The callback that will be called when the server starts.
        /// </summary>
        public Action OnServerStart { get; set; }

        /// <summary>
        ///     The callback that will be called when the server stops.
        /// </summary>
        public Action OnServerStop { get; set; }
        
        /// <summary>
        ///     Called when some error happened on the server.
        /// </summary>
        public Action<string> OnServerError { get; set; }
        
        /// <summary>
        ///     Called when some error happened on the client.
        /// </summary>
        public Action<string> OnClientError { get; set; }
        #endregion

        #region Management Methods
        /// <summary>
        ///     Starts the network transport as a host (server and client).
        /// </summary>
        /// <param name="magicNumber"></param>
        public abstract void StartHost(int magicNumber);

        /// <summary>
        ///     Starts the network transport as a server.
        /// </summary>
        public abstract void StartServer();

        /// <summary>
        ///     Starts the network transport as a client.
        /// </summary>
        /// <param name="magicNumber"></param>
        public abstract void StartClient(int magicNumber);

        /// <summary>
        ///     Closes the network transport.
        /// </summary>
        public abstract void Close();
        #endregion

        #region Server Client Methods
        /// <summary>
        ///     Returns the connection information of the client with the given id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [ServerOnly]
        public abstract IClientConnectionInfo GetClientInfo(int id);

        /// <summary>
        ///     Returns the number of clients connected to the server.
        /// </summary>
        /// <returns></returns>
        [ServerOnly]
        public abstract int GetClientCount();

        /// <summary>
        ///     Returns all the clients connected to the server.
        /// </summary>
        /// <returns></returns>
        [ServerOnly]
        public abstract IEnumerable<IClientConnectionInfo> GetClients();
        #endregion

        #region Lifecycle
        /// <summary>
        ///     Disconnects the client from the server.
        /// </summary>
        /// <param name="reason"></param>
        [ClientOnly]
        public abstract void ClientDisconnect(string reason);

        /// <summary>
        ///     Disconnects the client with the given id from the server.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="reason"></param>
        [ServerOnly]
        public abstract void ServerDisconnect(int id, string reason);

        /// <summary>
        ///     Sends a packet to the server.
        ///     You can choose if the packet should be reliable or not.
        ///     Reliable packets are guaranteed to be delivered, but they are a little slower.
        ///     Non-reliable packets are faster, but they are not guaranteed to be delivered.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ClientOnly]
        public abstract void ClientSendPacket(IPacket packet, bool reliable = false);

        /// <summary>
        ///     Sends a packet to the client with the given id.
        ///     You can choose if the packet should be reliable or not.
        ///     Reliable packets are guaranteed to be delivered, but they are a little slower.
        ///     Non-reliable packets are faster, but they are not guaranteed to be delivered.
        ///     If the target is -1, the packet will be sent to all clients connected to the server.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="target"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public abstract void ServerSendPacket(IPacket packet, int target = -1, bool reliable = false);

        /// <summary>
        ///     Broadcasts a packet to all clients connected to the server.
        ///     You can choose if the packet should be reliable or not.
        ///     Reliable packets are guaranteed to be delivered, but they are a little slower.
        ///     Non-reliable packets are faster, but they are not guaranteed to be delivered.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="reliable"></param>
        [ServerOnly]
        public void BroadcastServerPacket(IPacket packet, bool reliable = false)
        {
            ServerSendPacket(packet, -1, reliable);
        }
        #endregion
    }
}